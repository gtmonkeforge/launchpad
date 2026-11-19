using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Launchpad.Game;

public enum GamePlatform
{
    Steam,
    Meta,
    Custom,
}

public sealed record GameInstall(GamePlatform Platform, string Path)
{
    public string Label => Platform switch
    {
        GamePlatform.Steam => "SteamVR",
        GamePlatform.Meta => "Meta Horizon Link",
        _ => "Custom folder",
    };
}

public static class GameLocator
{
    public const string SteamAppId = "1533390";
    public const string Executable = "Gorilla Tag.exe";
    public const string MetaFolder = "another-axiom-gorilla-tag";

    public static bool IsGameFolder(string? path) => !string.IsNullOrWhiteSpace(path) && File.Exists(System.IO.Path.Combine(path, Executable));

    public static IReadOnlyList<GameInstall> Discover()
    {
        var found = new List<GameInstall>();

        foreach (var path in FindSteam(SteamRoots()))
            found.Add(new GameInstall(GamePlatform.Steam, path));

        if (OperatingSystem.IsWindows())
        {
            foreach (var path in FindMeta(MetaLibraries()))
                found.Add(new GameInstall(GamePlatform.Meta, path));
        }

        return found.DistinctBy(install => Normalize(install.Path)).ToList();
    }

    public static GamePlatform Classify(string path)
    {
        var normalized = Normalize(path).Replace('\\', '/');

        if (normalized.Contains("/steamapps/common/", StringComparison.OrdinalIgnoreCase))
            return GamePlatform.Steam;

        if (normalized.EndsWith("/" + MetaFolder, StringComparison.OrdinalIgnoreCase) || File.Exists(System.IO.Path.Combine(path, "OVRPlugin.dll")) && !File.Exists(System.IO.Path.Combine(path, "steam_api64.dll")))
            return GamePlatform.Meta;

        return File.Exists(System.IO.Path.Combine(path, "steam_api64.dll")) ? GamePlatform.Steam : GamePlatform.Custom;
    }

    private static string Normalize(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path).TrimEnd('\\', '/');
        }
        catch (Exception)
        {
            return path;
        }
    }

    public static IEnumerable<string> SteamRoots()
    {
        var roots = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            roots.AddRange(WindowsSteamRegistry());
            roots.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            roots.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg ? xdg : System.IO.Path.Combine(home, ".local/share");
            roots.Add(System.IO.Path.Combine(home, ".steam/steam"));
            roots.Add(System.IO.Path.Combine(home, ".steam/root"));
            roots.Add(System.IO.Path.Combine(data, "Steam"));
            roots.Add(System.IO.Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam"));
            roots.Add(System.IO.Path.Combine(home, "snap/steam/common/.local/share/Steam"));
        }

        return roots.Where(Directory.Exists).DistinctBy(Normalize);
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> WindowsSteamRegistry()
    {
        var paths = new List<string>();

        void Add(RegistryKey hive, string key, string value)
        {
            try
            {
                using RegistryKey? opened = hive.OpenSubKey(key);

                if (opened?.GetValue(value) is string path && path.Length > 0)
                    paths.Add(path.Replace('/', '\\'));
            }
            catch (Exception)
            {
            }
        }

        Add(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        Add(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        Add(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
        return paths;
    }

    public static IEnumerable<string> FindSteam(IEnumerable<string> roots)
    {
        var libraries = new List<string>();

        foreach (var root in roots)
        {
            libraries.Add(root);

            foreach (var file in new[] { "steamapps/libraryfolders.vdf", "config/libraryfolders.vdf" })
            {
                try
                {
                    List<Vdf.Node> vdf = Vdf.Parse(File.ReadAllText(System.IO.Path.Combine(root, file)));

                    foreach (Vdf.Node folder in Vdf.Find(vdf, "libraryfolders")?.Children ?? [])
                    {
                        var path = folder.Child("path")?.Value ?? (folder.Value != null && int.TryParse(folder.Key, out _) ? folder.Value : null);

                        if (!string.IsNullOrWhiteSpace(path))
                            libraries.Add(path);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
                {
                }
            }
        }

        foreach (var library in libraries.DistinctBy(Normalize))
        {
            var steamapps = System.IO.Path.Combine(library, "steamapps");
            var installDir = "Gorilla Tag";

            try
            {
                var manifest = System.IO.Path.Combine(steamapps, $"appmanifest_{SteamAppId}.acf");

                if (File.Exists(manifest) && Vdf.Find(Vdf.Parse(File.ReadAllText(manifest)), "AppState", "installdir")?.Value is { Length: > 0 } dir)
                    installDir = dir;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
            {
            }

            var game = System.IO.Path.Combine(steamapps, "common", installDir);

            if (IsGameFolder(game))
                yield return game;
        }
    }

    [SupportedOSPlatform("windows")]
    public static IEnumerable<string> MetaLibraries()
    {
        var libraries = new List<string>();

        try
        {
            using RegistryKey? root = Registry.CurrentUser.OpenSubKey(@"Software\Oculus VR, LLC\Oculus\Libraries");

            foreach (var id in root?.GetSubKeyNames() ?? [])
            {
                using RegistryKey? library = root!.OpenSubKey(id);

                foreach (var value in new[] { "OriginalPath", "Path" })
                {
                    if (library?.GetValue(value) is string path && path.Length > 0)
                        libraries.Add(path);
                }
            }
        }
        catch (Exception)
        {
        }

        foreach (var basePath in new[]
                 {
                     Environment.GetEnvironmentVariable("OculusBase"),
                     ReadMachine(@"SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus", "Base"),
                     ReadMachine(@"SOFTWARE\Oculus VR, LLC\Oculus", "Base"),
                     System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Oculus"),
                 })
        {
            if (!string.IsNullOrWhiteSpace(basePath))
                libraries.Add(System.IO.Path.Combine(basePath, "Software"));
        }

        return libraries.DistinctBy(Normalize);
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadMachine(string key, string value)
    {
        try
        {
            using RegistryKey? opened = Registry.LocalMachine.OpenSubKey(key);
            return opened?.GetValue(value) as string;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static IEnumerable<string> FindMeta(IEnumerable<string> libraries)
    {
        foreach (var library in libraries)
        {
            var software = System.IO.Path.Combine(library, "Software");
            var known = System.IO.Path.Combine(software, MetaFolder);

            if (IsGameFolder(known))
            {
                yield return known;
                continue;
            }

            string[] apps;

            try
            {
                apps = Directory.Exists(software) ? Directory.GetDirectories(software) : [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                apps = [];
            }

            foreach (var app in apps)
            {
                if (IsGameFolder(app))
                    yield return app;
            }
        }
    }
}
