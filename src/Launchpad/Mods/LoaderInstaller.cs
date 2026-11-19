using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Launchpad.Game;
using Launchpad.Infrastructure;

namespace Launchpad.Mods;

public sealed partial class LoaderInstaller(HttpClient http, Action<string> progress)
{
    private const long MaxPackageBytes = 80L * 1024 * 1024;
    public const string InjectorRecordPrefix = "launchpad-injector-";

    public static HttpClient CreateHttp()
    {
        var client = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All }) { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MonkeForge-Launchpad", "2.0"));
        return client;
    }

    public static bool IsInternalRecord(string guid) =>
        guid is "bepinex" or "melonloader" || guid.StartsWith(InjectorRecordPrefix, StringComparison.Ordinal);

    public static string InjectorRecord(ModLoader loader) => InjectorRecordPrefix + loader.Id();

    private static string DisabledProxy(string game, ModLoader loader) => Path.Combine(game, ".launchpad", "disabled-loaders", loader.Id(), loader.Proxy() + ".dll");

    private static string ActiveProxy(string game, ModLoader loader) => Path.Combine(game, loader.Proxy() + ".dll");

    public sealed record Status(bool RuntimeInstalled, bool Active, string? Version, bool InjectorDeployed);

    public static async Task<Status> StatusAsync(string game, ModLoader loader)
    {
        Manifest manifest = await Manifest.ReadAsync(game);
        var runtime = loader.RuntimeInstalled(game);
        var active = runtime && File.Exists(ActiveProxy(game, loader));
        var injector = manifest.Find(InjectorRecord(loader)) is { } record && record.Files.All(file => File.Exists(ModFiles.Resolve(game, file)));
        return new Status(runtime, active, manifest.Find(loader.RecordGuid())?.Version, injector);
    }

    public async Task EnsureAsync(string game, ModLoader loader, bool injector, CancellationToken ct)
    {
        if (OperatingSystem.IsLinux() && GameLocator.Classify(game) == GamePlatform.Steam)
            ConfigureProton(loader);

        var proxyPresent = File.Exists(ActiveProxy(game, loader)) || File.Exists(DisabledProxy(game, loader));

        if (!loader.RuntimeInstalled(game) || !proxyPresent)
            await InstallRuntimeAsync(game, loader, ct);

        Activate(game, loader);

        if (injector)
            await DeployInjectorAsync(game, loader);
    }

    public static Proton.Result? ConfigureProton(ModLoader loader)
    {
        try
        {
            return Proton.Configure(GameLocator.SteamRoots(), loader.Proxy());
        }
        catch (FormatException ex)
        {
            throw new UserFacingException(ex.Message);
        }
    }

    private sealed record Asset(string Name, long Size, string Url, string? Sha256);

    [GeneratedRegex(@"^BepInEx_win_x64_5\.[\d.]+\.zip$", RegexOptions.IgnoreCase)]
    private static partial Regex BepInExAsset();

    [GeneratedRegex(@"^(BepInEx/|winhttp\.dll$|doorstop_config\.ini$|\.doorstop_version$|changelog\.txt$)", RegexOptions.IgnoreCase)]
    private static partial Regex BepInExEntry();

    [GeneratedRegex(@"^(MelonLoader/|version\.dll$|dobby\.dll$|NOTICE(\.txt)?$|LICENSE(\.md|\.txt)?$)", RegexOptions.IgnoreCase)]
    private static partial Regex MelonEntry();

    public async Task<string> InstallRuntimeAsync(string game, ModLoader loader, CancellationToken ct)
    {
        progress($"Checking the latest {loader.DisplayName()}…");
        var repo = loader == ModLoader.MelonLoader ? "LavaGang/MelonLoader" : "BepInEx/BepInEx";
        (var version, Asset asset) = await LatestAsync(repo, name => loader == ModLoader.MelonLoader ? name == "MelonLoader.x64.zip" : BepInExAsset().IsMatch(name), ct);

        progress($"Downloading {loader.DisplayName()} {version}…");
        var bytes = await DownloadAsync(asset, ct);
        Dictionary<string, byte[]> archive = Archive.Read(bytes);
        Regex allowed = loader == ModLoader.MelonLoader ? MelonEntry() : BepInExEntry();

        var entries = archive.Where(entry => allowed.IsMatch(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        var proxy = loader.Proxy() + ".dll";

        if (!entries.ContainsKey(proxy) || !entries.Keys.Any(name => loader.RuntimeFiles().Contains(name, StringComparer.OrdinalIgnoreCase)))
            throw new UserFacingException($"The {loader.DisplayName()} package is missing files. Try again later.");

        progress($"Installing {loader.DisplayName()} {version}…");
        Manifest manifest = await Manifest.ReadAsync(game);

        SafeFile.Delete(DisabledProxy(game, loader));

        await ModFiles.ApplyAsync(game, manifest, new InstalledMod { Guid = loader.RecordGuid(), Name = loader.DisplayName(), Version = version, Loader = loader }, entries, adopt: true);

        foreach (var folder in loader.PluginFolders())
            Directory.CreateDirectory(Path.Combine(game, folder));

        return version;
    }

    private async Task<(string Version, Asset Asset)> LatestAsync(string repo, Func<string, bool> match, CancellationToken ct)
    {
        JsonNode release;

        try
        {
            using HttpResponseMessage response = await http.GetAsync($"https://api.github.com/repos/{repo}/releases/latest", ct);

            if (!response.IsSuccessStatusCode)
                throw new UserFacingException($"GitHub returned {(int)response.StatusCode} while checking {repo}. Try again in a few minutes.");

            release = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct)) ?? new JsonObject();
        }
        catch (HttpRequestException)
        {
            throw new UserFacingException("GitHub couldn't be reached to download the mod loader. Check your internet connection.");
        }
        catch (JsonException)
        {
            throw new UserFacingException("GitHub sent a response Launchpad couldn't read.");
        }

        var version = (release.Str("tag_name") ?? "").TrimStart('v');

        foreach (JsonNode? item in release["assets"] as JsonArray ?? [])
        {
            var name = item.Str("name") ?? "";

            if (!match(name))
                continue;

            var digest = item.Str("digest");
            var url = item.Str("browser_download_url") ?? "";

            if (!url.StartsWith("https://github.com/", StringComparison.Ordinal))
                break;

            return (version, new Asset(name, item.Long("size"), url, digest?.StartsWith("sha256:", StringComparison.Ordinal) == true ? digest[7..].ToLowerInvariant() : null));
        }

        throw new UserFacingException($"The latest release of {repo} has no Windows x64 package Launchpad can use.");
    }

    private async Task<byte[]> DownloadAsync(Asset asset, CancellationToken ct)
    {
        if (asset.Size <= 0 || asset.Size > MaxPackageBytes)
            throw new UserFacingException("The mod loader package is an unexpected size.");

        byte[] bytes;

        try
        {
            using HttpResponseMessage response = await http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            bytes = await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (HttpRequestException)
        {
            throw new UserFacingException("The mod loader download failed. Check your internet connection.");
        }

        if (bytes.Length != asset.Size)
            throw new UserFacingException("The mod loader download was incomplete. Try again.");

        if (asset.Sha256 != null && Convert.ToHexStringLower(SHA256.HashData(bytes)) != asset.Sha256)
            throw new UserFacingException("The mod loader download failed its checksum, so it wasn't installed.");

        return bytes;
    }

    public static void Activate(string game, ModLoader loader)
    {
        ModLoader inactive = loader.Other();
        string activeProxy = ActiveProxy(game, loader), savedProxy = DisabledProxy(game, loader);
        string inactiveProxy = ActiveProxy(game, inactive), parkedProxy = DisabledProxy(game, inactive);
        var moved = new List<(string From, string To)>();

        void Move(string from, string to)
        {
            Archive.EnsureNoLinks(game, from);
            Archive.EnsureNoLinks(game, to);

            if (File.Exists(to))
            {
                var previous = to + ".previous-" + Guid.NewGuid().ToString("N");
                File.Move(to, previous);
                moved.Add((to, previous));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Move(from, to);
            moved.Add((from, to));
        }

        try
        {
            if (File.Exists(activeProxy) && File.Exists(savedProxy))
                File.Delete(savedProxy);

            if (File.Exists(inactiveProxy) && inactive.RuntimeInstalled(game))
                Move(inactiveProxy, parkedProxy);

            if (!File.Exists(activeProxy) && File.Exists(savedProxy))
                Move(savedProxy, activeProxy);

            if (!File.Exists(activeProxy))
                throw new UserFacingException($"{loader.DisplayName()}'s {loader.Proxy()}.dll is missing. Reinstall the loader in Settings.");
        }
        catch
        {
            for (var i = moved.Count - 1; i >= 0; i--)
            {
                try
                {
                    File.Move(moved[i].To, moved[i].From, true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            throw;
        }
    }

    public static string PluginPath(ModLoader loader) => loader == ModLoader.MelonLoader
        ? "Plugins/MonkeForge.Injector.MelonLoader.dll"
        : "BepInEx/plugins/MonkeForge.Injector/MonkeForge.Injector.BepInEx.dll";

    public static byte[] EmbeddedPlugin(ModLoader loader)
    {
        var name = "injector/" + Path.GetFileName(PluginPath(loader));
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);

        if (stream == null)
            throw new UserFacingException("This build of Launchpad doesn't include the injector plugin.");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public async Task DeployInjectorAsync(string game, ModLoader loader)
    {
        var plugin = EmbeddedPlugin(loader);
        var target = Archive.SafePath(game, PluginPath(loader));
        Manifest manifest = await Manifest.ReadAsync(game);
        var version = Convert.ToHexStringLower(SHA256.HashData(plugin))[..12];

        if (manifest.Find(InjectorRecord(loader))?.Version == version && File.Exists(target))
            return;

        progress("Installing the MonkeForge injector…");
        var record = new InstalledMod { Guid = InjectorRecord(loader), Name = "MonkeForge Injector", Version = version, Loader = loader };
        await ModFiles.ApplyAsync(game, manifest, record, new Dictionary<string, byte[]> { [PluginPath(loader)] = plugin }, adopt: true);
    }

    public static async Task RemoveInjectorAsync(string game, ModLoader loader)
    {
        Manifest manifest = await Manifest.ReadAsync(game);

        if (manifest.Find(InjectorRecord(loader)) is { } record)
            await ModFiles.ApplyAsync(game, manifest, record, new Dictionary<string, byte[]>(), removeOnly: true);
    }
}
