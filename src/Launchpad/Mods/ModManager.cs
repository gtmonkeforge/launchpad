using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Launchpad.Forge;
using Launchpad.Game;
using Launchpad.Infrastructure;
using Launchpad.Injector;

namespace Launchpad.Mods;

public sealed record InstalledView(string Guid, string Name, string Version, string Loader, string Channel, bool Injector, int Files, bool Custom);

public sealed record UpdateInfo(string Guid, string Installed, string? Latest, bool Available);

public sealed partial class ModManager(ForgeClient forge, PayloadClient payloads, LoaderInstaller loaders, Func<Settings> settings, Action<string> progress)
{
    private const long MaxDownloadBytes = 200L * 1024 * 1024;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action? Changed;

    internal Func<bool> GameRunning { get; init; } = GameProcess.IsRunning;

    [GeneratedRegex("^[A-Za-z0-9._-]{1,128}$")]
    private static partial Regex GuidPattern();

    [GeneratedRegex("^[a-z0-9-]{1,40}$")]
    private static partial Regex ChannelPattern();

    public string Game()
    {
        var path = settings().GamePath;

        if (!GameLocator.IsGameFolder(path))
            throw new UserFacingException("Choose your Gorilla Tag folder in Settings first.");

        return path!;
    }

    public async Task<List<InstalledView>> ListAsync()
    {
        var path = settings().GamePath;

        if (!GameLocator.IsGameFolder(path))
            return [];

        Manifest manifest = await Manifest.ReadAsync(path!);
        return manifest.Installed
            .Where(mod => !LoaderInstaller.IsInternalRecord(mod.Guid))
            .Select(View)
            .OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<T> LockedAsync<T>(Func<Task<T>> work)
    {
        if (!await _lock.WaitAsync(0))
            throw new UserFacingException("Launchpad is already changing mods. Wait for it to finish.");

        try
        {
            return await work();
        }
        finally
        {
            _lock.Release();
            Changed?.Invoke();
        }
    }

    private void RequireGameClosed()
    {
        if (GameRunning())
            throw new UserFacingException("Close Gorilla Tag before changing mod files.");
    }

    public static string ModPath(string guid, string channel) =>
        $"/api/v1/mods/{Uri.EscapeDataString(guid)}?channel={Uri.EscapeDataString(channel)}";

    public Task<InstalledView> InstallAsync(string guid, string? channel, CancellationToken ct) => LockedAsync(async () =>
    {
        var game = Game();
        Settings current = settings();
        ModLoader loader = current.Loader;
        channel ??= current.Channels.GetValueOrDefault(guid) ?? "stable";

        if (!GuidPattern().IsMatch(guid) || !ChannelPattern().IsMatch(channel))
            throw new UserFacingException("That isn't a valid mod.");

        progress("Checking the mod…");
        JsonNode mod = await forge.GetJsonAsync(ModPath(guid, channel), ct);
        var realGuid = mod.Str("guid") ?? "";
        var name = mod.Str("name") ?? realGuid;

        if (!GuidPattern().IsMatch(realGuid))
            throw new UserFacingException("MonkeForge sent an invalid mod.");

        if (mod.Bool("gated") && !mod.Bool("owned"))
        {
            throw forge.User == null
                ? new UserFacingException($"{name} is a paid mod. Sign in, then buy it to install it.")
                : new UserFacingException($"You don't own {name} yet. Buy it first, then install it.");
        }

        JsonNode release = (mod["releases"] as JsonArray)?.FirstOrDefault() ?? throw new UserFacingException($"{name} has no release on the {channel} channel yet.");
        var version = release.Str("version") ?? "";
        var record = new InstalledMod { Guid = realGuid, Name = name, Version = version, Loader = loader, Channel = channel };
        Manifest manifest = await Manifest.ReadAsync(game);
        InstalledMod? previous = manifest.Find(realGuid);

        if (previous is { Custom: true })
            throw new UserFacingException($"A custom mod named {previous.Name} is in the way. Remove it from your Library first.");

        if (previous != null && previous.EffectiveLoader != loader)
            throw new UserFacingException($"{name} is installed for {previous.EffectiveLoader.DisplayName()}. Remove it first, or switch back to {previous.EffectiveLoader.DisplayName()} in Settings.");

        if (mod.Bool("uses_injector"))
        {
            if (forge.User == null)
                throw new UserFacingException($"{name} is delivered through the MonkeForge injector. Sign in to install it.");

            if (!(release["loaders"] as JsonArray ?? []).Any(item => item?.GetValue<string>() == loader.Id()))
                throw new UserFacingException($"{name} isn't available for {loader.DisplayName()}. Switch loaders in Settings.");

            if (previous is { Injector: false, Files.Count: > 0 })
                RequireGameClosed();

            await loaders.EnsureAsync(game, loader, true, ct);

            progress($"Verifying {name}…");

            using (Payload payload = await payloads.FetchAsync(realGuid, channel, loader.Id(), ct))
                record.Version = payload.Version;

            record.Injector = true;
            manifest = await Manifest.ReadAsync(game);
            await ModFiles.ApplyAsync(game, manifest, record, new Dictionary<string, byte[]>());
        }
        else
        {
            JsonNode file = SelectDownload(release, loader) ?? throw new UserFacingException($"{name} {version} has no {loader.DisplayName()} download. Switch loaders in Settings.");
            var bytes = await DownloadAsync(file, name, ct);
            var kind = file.Str("kind") ?? "";
            Dictionary<string, byte[]> entries = kind == "zip"
                ? Archive.ModEntries(Archive.Read(bytes), loader, realGuid)
                : new Dictionary<string, byte[]>
                {
                    [(loader == ModLoader.MelonLoader ? "Mods/" : $"BepInEx/plugins/{realGuid}/") + Archive.SafeFileName(file.Str("filename") ?? "", realGuid + ".dll")] = bytes,
                };

            RequireGameClosed();
            await loaders.EnsureAsync(game, loader, false, ct);
            progress($"Installing {name}…");
            manifest = await Manifest.ReadAsync(game);
            await ModFiles.ApplyAsync(game, manifest, record, entries);
        }

        current.Channels[realGuid] = channel;
        await current.SaveAsync();
        return View(record);
    });

    public const long MaxCustomBytes = 64L * 1024 * 1024;
    public const string CustomPrefix = "custom.";

    public Task<InstalledView> InstallFileAsync(string fileName, byte[] bytes, CancellationToken ct) => LockedAsync(async () =>
    {
        var game = Game();
        ModLoader loader = settings().Loader;

        if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new UserFacingException("Only .dll files can be installed as custom mods.");

        if (bytes.Length == 0 || bytes.Length > MaxCustomBytes)
            throw new UserFacingException($"{Path.GetFileName(fileName)} is empty or too large to install.");

        (var assemblyName, var version) = ReadAssembly(bytes, Path.GetFileName(fileName));
        var file = Archive.SafeFileName(Path.GetFileName(fileName), assemblyName + ".dll");

        if (!file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            file += ".dll";

        var slug = SlugPattern().Replace(Path.GetFileNameWithoutExtension(file), "-").Trim('-', '.');
        var guid = CustomPrefix + (slug.Length is > 0 and <= 100 ? slug : "mod");
        var record = new InstalledMod { Guid = guid, Name = assemblyName, Version = version, Loader = loader, Custom = true };

        Manifest manifest = await Manifest.ReadAsync(game);
        InstalledMod? previous = manifest.Find(guid);

        if (previous is { Custom: false })
            throw new UserFacingException($"{file} conflicts with {previous.Name}, which was installed from MonkeForge.");

        if (previous != null && previous.EffectiveLoader != loader)
            throw new UserFacingException($"{file} is installed for {previous.EffectiveLoader.DisplayName()}. Remove it first, or switch back to {previous.EffectiveLoader.DisplayName()} in Settings.");

        var entries = new Dictionary<string, byte[]>
        {
            [(loader == ModLoader.MelonLoader ? "Mods/" : $"BepInEx/plugins/{guid}/") + file] = bytes,
        };

        RequireGameClosed();
        await loaders.EnsureAsync(game, loader, false, ct);
        progress($"Installing {assemblyName}…");
        manifest = await Manifest.ReadAsync(game);
        await ModFiles.ApplyAsync(game, manifest, record, entries);
        return View(record);
    });

    [GeneratedRegex("[^A-Za-z0-9._-]+")]
    private static partial Regex SlugPattern();

    private static (string Name, string Version) ReadAssembly(byte[] bytes, string fileName)
    {
        try
        {
            using var pe = new System.Reflection.PortableExecutable.PEReader(new MemoryStream(bytes));

            if (!pe.HasMetadata)
                throw new UserFacingException($"{fileName} isn't a .NET assembly, so it can't be loaded as a mod.");

            System.Reflection.Metadata.MetadataReader metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);

            if (!metadata.IsAssembly)
                throw new UserFacingException($"{fileName} isn't a .NET assembly, so it can't be loaded as a mod.");

            System.Reflection.Metadata.AssemblyDefinition definition = metadata.GetAssemblyDefinition();
            var name = metadata.GetString(definition.Name);
            return (string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(fileName) : name, definition.Version.ToString(3));
        }
        catch (Exception ex) when (ex is BadImageFormatException or InvalidOperationException)
        {
            throw new UserFacingException($"{fileName} isn't a valid .NET assembly, so it can't be loaded as a mod.");
        }
    }

    public Task<bool> UninstallAsync(string guid) => LockedAsync(async () =>
    {
        if (LoaderInstaller.IsInternalRecord(guid))
            throw new UserFacingException("Mod loaders can't be removed as mods.");

        var game = Game();
        Manifest manifest = await Manifest.ReadAsync(game);
        InstalledMod record = manifest.Find(guid) ?? throw new UserFacingException("That mod isn't installed.");

        if (record.Files.Count > 0 || record.Folders.Count > 0)
            RequireGameClosed();

        progress($"Removing {record.Name}…");
        await ModFiles.ApplyAsync(game, manifest, record, new Dictionary<string, byte[]>(), removeOnly: true);

        if (record.Injector && !manifest.Installed.Any(mod => mod.Injector && mod.EffectiveLoader == record.EffectiveLoader) && !GameRunning())
            await LoaderInstaller.RemoveInjectorAsync(game, record.EffectiveLoader);

        settings().Channels.Remove(guid);
        await settings().SaveAsync();
        return true;
    });

    public async Task<List<UpdateInfo>> CheckUpdatesAsync(CancellationToken ct)
    {
        var results = new List<UpdateInfo>();

        foreach (InstalledView mod in await ListAsync())
        {
            if (mod.Custom)
                continue;

            string? latest = null;

            try
            {
                JsonNode detail = await forge.GetJsonAsync(ModPath(mod.Guid, mod.Channel), ct);
                latest = detail.Str("current_version") ?? (detail["releases"] as JsonArray)?.FirstOrDefault().Str("version");
            }
            catch (ForgeException ex) when (ex.Status is 403 or 404)
            {
            }

            results.Add(new UpdateInfo(mod.Guid, mod.Version, latest, latest != null && latest != mod.Version));
        }

        return results;
    }

    public static JsonNode? SelectDownload(JsonNode release, ModLoader loader)
    {
        IEnumerable<JsonNode?> files = release["downloads_by_loader"]?[loader.Id()] is JsonArray byLoader
            ? byLoader
            : (release["downloads"] as JsonArray ?? []).Where(file => file.Str("loader") == loader.Id() || (loader == ModLoader.Bepinex5 && string.IsNullOrEmpty(file.Str("loader"))));

        return files.FirstOrDefault(file => file.Str("kind") is "dll" or "zip" && !string.IsNullOrEmpty(file.Str("url")));
    }

    private async Task<byte[]> DownloadAsync(JsonNode file, string name, CancellationToken ct)
    {
        var size = file.Long("size");
        var expected = (file.Str("sha256") ?? "").ToLowerInvariant();

        if (size > MaxDownloadBytes)
            throw new UserFacingException($"{name} is too large to install.");

        progress($"Downloading {name}…");
        byte[] bytes;

        try
        {
            using HttpResponseMessage response = await forge.SendAsync(HttpMethod.Get, file.Str("url")!, null, false, ct);

            if (response.Content.Headers.ContentLength > MaxDownloadBytes)
                throw new UserFacingException($"{name} is too large to install.");

            bytes = await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (ForgeException ex) when (ex.Status == 402)
        {
            throw new UserFacingException($"You don't own {name} yet. Buy it first, then install it.");
        }

        if (size > 0 && bytes.Length != size)
            throw new UserFacingException($"The download of {name} was incomplete. Try again.");

        if (expected.Length == 64 && Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
            throw new UserFacingException($"{name} failed its integrity check, so it wasn't installed.");

        return bytes;
    }

    private static InstalledView View(InstalledMod mod) => new(mod.Guid, mod.Name, mod.Version, mod.EffectiveLoader.Id(), mod.Channel ?? "stable", mod.Injector, mod.Files.Count, mod.Custom);
}
