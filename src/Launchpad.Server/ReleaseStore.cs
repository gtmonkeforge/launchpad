using System.Security.Cryptography;
using System.Text;
using Launchpad.Updates;

namespace Launchpad.Server;

// Releases live on disk, one folder per version:
//
//   data/releases/2.1.0/release.json       the manifest, exactly as signed
//   data/releases/2.1.0/release.json.sig   base64 ECDSA P-256 signature over release.json
//   data/releases/2.1.0/<installer files>
//
// Uploading through PUT /api/v1/releases/{version} writes these, but copying a folder in by hand works just as well.
// Published versions are immutable: a signed manifest should always describe the same bytes.
public sealed class ReleaseStore(ServerOptions options, ILogger<ReleaseStore> logger)
{
    public sealed record Release(ReleaseManifest Manifest, byte[] ManifestBytes, string Signature, string Folder);

    private readonly object _gate = new();
    private Dictionary<string, (DateTime Stamp, Release? Release)> _cache = new(StringComparer.Ordinal);
    private DateTime _scanned = DateTime.MinValue;
    private IReadOnlyList<Release> _releases = [];

    public IReadOnlyList<Release> All()
    {
        lock (_gate)
        {
            if (DateTime.UtcNow - _scanned < TimeSpan.FromSeconds(15))
                return _releases;

            _scanned = DateTime.UtcNow;
            var next = new Dictionary<string, (DateTime, Release?)>(StringComparer.Ordinal);
            var found = new List<Release>();

            if (Directory.Exists(options.ReleasesDir))
            {
                foreach (var folder in Directory.EnumerateDirectories(options.ReleasesDir))
                {
                    var name = Path.GetFileName(folder);

                    if (!ReleaseManifest.TryParseVersion(name, out _))
                        continue;

                    var manifestPath = Path.Combine(folder, "release.json");
                    DateTime stamp = File.Exists(manifestPath) ? File.GetLastWriteTimeUtc(manifestPath) : DateTime.MinValue;
                    Release? release = _cache.TryGetValue(name, out var cached) && cached.Stamp == stamp ? cached.Release : Load(folder, name);
                    next[name] = (stamp, release);

                    if (release != null)
                        found.Add(release);
                }
            }

            _cache = next;
            _releases = found.OrderByDescending(r => r.Manifest.ParsedVersion).ToList();
            return _releases;
        }
    }

    private Release? Load(string folder, string name)
    {
        try
        {
            var bytes = File.ReadAllBytes(Path.Combine(folder, "release.json"));
            var signature = File.ReadAllText(Path.Combine(folder, "release.json.sig")).Trim();
            ReleaseManifest manifest = ReleaseManifest.Parse(bytes);

            if (manifest.Version != name)
                throw new FormatException($"release.json says {manifest.Version} but it's in the {name} folder.");

            if (options.ReleasePublicKey.Length > 0 && !ReleaseManifest.VerifySignature(bytes, signature, options.ReleasePublicKey))
                throw new FormatException("release.json.sig doesn't match RELEASE_PUBLIC_KEY.");

            foreach (ReleaseFile file in manifest.Files)
            {
                var info = new FileInfo(Path.Combine(folder, file.Name));

                if (!info.Exists || info.Length != file.Size)
                    throw new FormatException($"{file.Name} is missing or isn't {file.Size} bytes.");
            }

            return new Release(manifest, bytes, signature, folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            logger.LogWarning("Skipping release {Version}: {Reason}", name, ex.Message);
            return null;
        }
    }

    public Release? Latest(string os, string arch) => All().FirstOrDefault(r => r.Manifest.FileFor(os, arch) != null);

    public Release? Find(string version) => All().FirstOrDefault(r => r.Manifest.Version == version);

    public sealed class PublishException(int status, string message) : Exception(message)
    {
        public int Status { get; } = status;
    }

    // Stores an uploaded release after checking every file against the manifest. Nothing becomes visible until the
    // whole folder is in place: it's written to a staging folder and moved in one step.
    public async Task PublishAsync(string version, IFormCollection form, CancellationToken ct)
    {
        if (!ReleaseManifest.TryParseVersion(version, out _))
            throw new PublishException(400, "Use a version like 2.1.0.");

        IFormFile manifestFile = form.Files.GetFile("manifest") ?? throw new PublishException(400, "Attach release.json as the manifest field.");
        var signature = form["signature"].ToString().Trim();

        if (manifestFile.Length > ReleaseManifest.MaxBytes)
            throw new PublishException(400, "release.json is too large.");

        byte[] manifestBytes;

        using (var buffer = new MemoryStream())
        {
            await manifestFile.CopyToAsync(buffer, ct);
            manifestBytes = buffer.ToArray();
        }

        ReleaseManifest manifest;

        try
        {
            manifest = ReleaseManifest.Parse(manifestBytes);
        }
        catch (FormatException ex)
        {
            throw new PublishException(400, ex.Message);
        }

        if (manifest.Version != version)
            throw new PublishException(400, $"release.json is for {manifest.Version}, not {version}.");

        if (options.ReleasePublicKey.Length > 0 && !ReleaseManifest.VerifySignature(manifestBytes, signature, options.ReleasePublicKey))
            throw new PublishException(400, "The signature doesn't match RELEASE_PUBLIC_KEY. Sign release.json with the release key.");

        if (signature.Length == 0)
            throw new PublishException(400, "Send the base64 signature of release.json as the signature field.");

        var target = Path.Combine(options.ReleasesDir, version);

        if (Directory.Exists(target))
            throw new PublishException(409, $"{version} is already published. Releases can't be replaced; publish a new version.");

        Directory.CreateDirectory(options.ReleasesDir);
        var staging = Path.Combine(options.ReleasesDir, ".staging-" + version + "-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4)));
        Directory.CreateDirectory(staging);

        try
        {
            var uploads = form.Files.Where(f => f.Name == "file").ToList();

            if (uploads.Count != manifest.Files.Count)
                throw new PublishException(400, $"release.json lists {manifest.Files.Count} files but {uploads.Count} were uploaded.");

            foreach (ReleaseFile expected in manifest.Files)
            {
                IFormFile upload = uploads.FirstOrDefault(f => f.FileName == expected.Name)
                    ?? throw new PublishException(400, $"{expected.Name} wasn't uploaded.");

                if (upload.Length != expected.Size)
                    throw new PublishException(400, $"{expected.Name} is {upload.Length} bytes, but release.json says {expected.Size}.");

                await using (FileStream output = File.Create(Path.Combine(staging, expected.Name)))
                await using (Stream input = upload.OpenReadStream())
                {
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[81920];
                    int read;

                    while ((read = await input.ReadAsync(buffer, ct)) > 0)
                    {
                        hash.AppendData(buffer, 0, read);
                        await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    }

                    if (Convert.ToHexStringLower(hash.GetHashAndReset()) != expected.Sha256)
                        throw new PublishException(400, $"{expected.Name} doesn't match the SHA-256 in release.json.");
                }
            }

            await File.WriteAllBytesAsync(Path.Combine(staging, "release.json"), manifestBytes, ct);
            await File.WriteAllTextAsync(Path.Combine(staging, "release.json.sig"), signature + "\n", Encoding.ASCII, ct);
            Directory.Move(staging, target);
        }
        catch
        {
            TryDelete(staging);
            throw;
        }

        lock (_gate)
            _scanned = DateTime.MinValue;

        logger.LogInformation("Published Launchpad {Version}", version);
    }

    private static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
