using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Launchpad.Updates;

// Describes one Launchpad release: its version, notes, and the installer for each platform with its size and SHA-256.
// The release script signs the exact bytes of release.json with the offline release key; Launchpad only installs
// what that signature covers, so the update server can be trusted with distribution but not with code.
// Shared with Launchpad.Server, which links this file.
public sealed partial record ReleaseManifest(string Product, string Version, string PublishedAt, string Notes, IReadOnlyList<ReleaseFile> Files)
{
    public const string ProductName = "launchpad";
    public const int MaxBytes = 256 * 1024;

    public static readonly string[] Platforms = ["windows", "linux"];
    public static readonly string[] Architectures = ["x64", "arm64"];

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = true,
    };

    [GeneratedRegex(@"\A(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\z")]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z")]
    private static partial Regex FileNamePattern();

    [GeneratedRegex(@"\A[0-9a-f]{64}\z")]
    private static partial Regex Sha256Pattern();

    public static bool TryParseVersion(string? text, out System.Version version)
    {
        version = new System.Version(0, 0, 0);

        if (text == null || !VersionPattern().IsMatch(text))
            return false;

        version = System.Version.Parse(text);
        return true;
    }

    public static bool IsSafeFileName(string name) => FileNamePattern().IsMatch(name) && !name.Contains("..", StringComparison.Ordinal);

    // Parses and checks a manifest. Throws FormatException with a reason when anything is missing or out of range.
    public static ReleaseManifest Parse(ReadOnlySpan<byte> json)
    {
        if (json.Length is 0 or > MaxBytes)
            throw new FormatException("release.json is empty or too large.");

        ReleaseManifest? manifest;

        try
        {
            manifest = JsonSerializer.Deserialize<ReleaseManifest>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException("release.json isn't valid JSON: " + ex.Message);
        }

        if (manifest == null || manifest.Product != ProductName)
            throw new FormatException("release.json isn't a Launchpad release.");

        if (!TryParseVersion(manifest.Version, out _))
            throw new FormatException("release.json needs a version like 2.1.0.");

        if (!DateTimeOffset.TryParse(manifest.PublishedAt, out _))
            throw new FormatException("release.json needs published_at as an ISO 8601 date.");

        if (manifest.Notes == null || manifest.Notes.Length > 20_000)
            throw new FormatException("release.json notes are missing or longer than 20,000 characters.");

        if (manifest.Files == null || manifest.Files.Count is 0 or > 16)
            throw new FormatException("release.json must list between 1 and 16 files.");

        foreach (ReleaseFile file in manifest.Files)
        {
            if (file == null || !Platforms.Contains(file.Os) || !Architectures.Contains(file.Arch))
                throw new FormatException("Each file needs os (windows or linux) and arch (x64 or arm64).");

            if (file.Name == null || !IsSafeFileName(file.Name))
                throw new FormatException($"{file.Name} isn't a safe file name.");

            if (file.Size is <= 0 or > 2L * 1024 * 1024 * 1024)
                throw new FormatException($"{file.Name} has an invalid size.");

            if (file.Sha256 == null || !Sha256Pattern().IsMatch(file.Sha256))
                throw new FormatException($"{file.Name} needs a lowercase hex SHA-256.");
        }

        if (manifest.Files.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Files.Count
            || manifest.Files.Select(f => (f.Os, f.Arch)).Distinct().Count() != manifest.Files.Count)
            throw new FormatException("release.json lists a file name or platform twice.");

        return manifest;
    }

    public System.Version ParsedVersion => System.Version.Parse(Version);

    public ReleaseFile? FileFor(string os, string arch) => Files.FirstOrDefault(f => f.Os == os && f.Arch == arch);

    // ECDSA P-256 over SHA-256, IEEE P1363 signature, public key as base64 SubjectPublicKeyInfo.
    public static bool VerifySignature(ReadOnlySpan<byte> manifest, string? signatureBase64, string publicKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(signatureBase64) || signatureBase64.Length > 512)
            return false;

        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);

            if (key.KeySize != 256)
                return false;

            return key.VerifyData(manifest, Convert.FromBase64String(signatureBase64.Trim()), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }
}

public sealed record ReleaseFile(string Os, string Arch, string Name, long Size, string Sha256);
