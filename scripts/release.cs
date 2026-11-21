#:property PublishAot=false

// Signs and publishes Launchpad releases for the update server (src/Launchpad.Server).
//
//   dotnet run scripts/release.cs -- keygen                      make the release key (once); prints the public key
//   dotnet run scripts/release.cs -- sign [notes.md]             write dist/release.json and dist/release.json.sig
//   dotnet run scripts/release.cs -- publish [notes.md]          sign, then upload to the update server
//
// The private key lives outside the repo, at $LAUNCHPAD_RELEASE_KEY or ~/.monkeforge/launchpad-release-key.pem.
// Anyone with it can ship code to every Launchpad install, so keep it off servers and back it up somewhere safe.
// publish reads the server from $LAUNCHPAD_APP_SERVER (default https://launchpad.monkeforge.org) and the
// upload token from $LAUNCHPAD_PUBLISH_TOKEN (the server's PUBLISH_TOKEN).

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var root = FindRoot();
var dist = Path.Combine(root, "dist");
var keyPath = Environment.GetEnvironmentVariable("LAUNCHPAD_RELEASE_KEY") is { Length: > 0 } custom
    ? Path.GetFullPath(custom)
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".monkeforge", "launchpad-release-key.pem");

switch (args.FirstOrDefault())
{
    case "keygen":
        return KeyGen();
    case "sign":
        return Sign(args.ElementAtOrDefault(1)) == null ? 1 : 0;
    case "publish":
        return await PublishAsync(args.ElementAtOrDefault(1));
    case "pubkey":
        return PrintPublicKey();
    default:
        Console.Error.WriteLine("Usage: dotnet run scripts/release.cs -- keygen | pubkey | sign [notes.md] | publish [notes.md]");
        return 1;
}

int KeyGen()
{
    if (File.Exists(keyPath))
    {
        Console.Error.WriteLine($"{keyPath} already exists. Delete it first if you really mean to replace the release key; installed copies of Launchpad will then refuse updates signed with the new one.");
        return 1;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
    using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    File.WriteAllText(keyPath, key.ExportPkcs8PrivateKeyPem() + "\n");

    if (!OperatingSystem.IsWindows())
        File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

    Console.WriteLine($"Wrote the release key to {keyPath}. Back it up; it can't be recovered.");
    Console.WriteLine("Public key (put it in src/Launchpad/Updates/Updater.cs and the server's RELEASE_PUBLIC_KEY):");
    Console.WriteLine(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
    return 0;
}

int PrintPublicKey()
{
    using ECDsa key = LoadKey();
    Console.WriteLine(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
    return 0;
}

ECDsa LoadKey()
{
    if (!File.Exists(keyPath))
        throw new InvalidOperationException($"No release key at {keyPath}. Run keygen first, or set LAUNCHPAD_RELEASE_KEY.");

    var key = ECDsa.Create();
    key.ImportFromPem(File.ReadAllText(keyPath));
    return key;
}

(string Version, byte[] Manifest, string Signature, List<string> Files)? Sign(string? notesPath)
{
    var version = XDocument.Load(Path.Combine(root, "Directory.Build.props")).Descendants("Version").First().Value.Trim();
    var notes = notesPath != null ? File.ReadAllText(notesPath).Trim() : $"Launchpad {version}";

    var expected = new (string Os, string Arch, string Name)[]
    {
        ("windows", "x64", $"launchpad-{version}-windows-x64.msi"),
        ("linux", "x64", $"launchpad-{version}-linux-x64.tar.gz"),
    };

    var files = new JsonArray();
    var paths = new List<string>();

    foreach (var (os, arch, name) in expected)
    {
        var path = Path.Combine(dist, name);

        if (!File.Exists(path))
        {
            Console.WriteLine($"Skipping {os}: {name} isn't in dist/. Run scripts/package.ps1 to build it.");
            continue;
        }

        using FileStream stream = File.OpenRead(path);
        files.Add(new JsonObject
        {
            ["os"] = os,
            ["arch"] = arch,
            ["name"] = name,
            ["size"] = stream.Length,
            ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(stream)),
        });
        paths.Add(path);
    }

    if (files.Count == 0)
    {
        Console.Error.WriteLine($"No installers for {version} in {dist}. Run scripts/package.ps1 first.");
        return null;
    }

    var manifest = new JsonObject
    {
        ["product"] = "launchpad",
        ["version"] = version,
        ["published_at"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        ["notes"] = notes,
        ["files"] = files,
    };

    var bytes = Encoding.UTF8.GetBytes(manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    using ECDsa key = LoadKey();
    var signature = Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

    File.WriteAllBytes(Path.Combine(dist, "release.json"), bytes);
    File.WriteAllText(Path.Combine(dist, "release.json.sig"), signature + "\n");
    Console.WriteLine($"Signed Launchpad {version} ({files.Count} file{(files.Count == 1 ? "" : "s")}) with public key {Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())}");
    return (version, bytes, signature, paths);
}

async Task<int> PublishAsync(string? notesPath)
{
    var token = Environment.GetEnvironmentVariable("LAUNCHPAD_PUBLISH_TOKEN");

    if (string.IsNullOrEmpty(token))
    {
        Console.Error.WriteLine("Set LAUNCHPAD_PUBLISH_TOKEN to the update server's PUBLISH_TOKEN.");
        return 1;
    }

    if (Sign(notesPath) is not { } release)
        return 1;

    var server = (Environment.GetEnvironmentVariable("LAUNCHPAD_APP_SERVER") is { Length: > 0 } s ? s : "https://launchpad.monkeforge.org").TrimEnd('/');
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
    using var form = new MultipartFormDataContent();
    form.Add(new ByteArrayContent(release.Manifest) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } }, "manifest", "release.json");
    form.Add(new StringContent(release.Signature), "signature");

    var streams = new List<FileStream>();

    try
    {
        foreach (var path in release.Files)
        {
            FileStream stream = File.OpenRead(path);
            streams.Add(stream);
            form.Add(new StreamContent(stream) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } }, "file", Path.GetFileName(path));
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, $"{server}/api/v1/releases/{release.Version}") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Console.WriteLine($"Uploading Launchpad {release.Version} to {server}…");
        using HttpResponseMessage response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var message = Regex.Match(body, "\"error\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"") is { Success: true } m ? Regex.Unescape(m.Groups[1].Value) : body;
            Console.Error.WriteLine($"The server refused the release ({(int)response.StatusCode}): {message}");
            return 1;
        }

        Console.WriteLine($"Published Launchpad {release.Version}. Installed copies pick it up within a few hours, or right away from Settings → About Launchpad.");
        return 0;
    }
    finally
    {
        streams.ForEach(stream => stream.Dispose());
    }
}

static string FindRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "Launchpad.slnx")))
            return dir.FullName;
    }

    throw new InvalidOperationException("Run this from inside the Launchpad repository.");
}
