using System.IO.Compression;
using System.Text.RegularExpressions;
using Launchpad.Infrastructure;

namespace Launchpad.Mods;

public static partial class Archive
{
    public const long MaxExtractedBytes = 200L * 1024 * 1024;
    public const int MaxEntries = 10_000;

    [GeneratedRegex(@"^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\.|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ReservedName();

    public static string SafePath(string root, string path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\0') || path.Contains(':') || path[0] is '/' or '\\' || Regex.IsMatch(path, @"(^|[\\/])\.\.([\\/]|$)"))
            throw new UserFacingException("The mod contains an unsafe file path.");

        foreach (var part in path.Split('/', '\\'))
        {
            if (part.Length == 0 || part.EndsWith('.') || part.EndsWith(' ') || ReservedName().IsMatch(part) || part.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0)
                throw new UserFacingException("The mod contains a file name Windows can't use: " + part);
        }

        var full = Path.GetFullPath(Path.Combine(root, path.Replace('\\', '/')));
        var relative = Path.GetRelativePath(Path.GetFullPath(root), full);

        if (relative == "." || relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new UserFacingException("The mod tried to write outside the game folder.");

        return full;
    }

    public static void EnsureNoLinks(string root, string target)
    {
        var current = Path.GetFullPath(root);

        foreach (var part in Path.GetRelativePath(current, target).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (IsLink(current))
                throw new UserFacingException("The game folder contains a symbolic link Launchpad won't write through.");

            current = Path.Combine(current, part);
        }

        if (IsLink(target))
            throw new UserFacingException("A mod file in the game folder is a symbolic link, so Launchpad left it alone.");
    }

    private static bool IsLink(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists || Directory.Exists(path) ? (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 : false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static Dictionary<string, byte[]> Read(byte[] bytes)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        long total = 0;

        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

            if (zip.Entries.Count > MaxEntries)
                throw new UserFacingException("The mod archive has too many files.");

            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');

                if (name.EndsWith('/'))
                    continue;

                SafePath(Path.GetTempPath(), name);
                total += entry.Length;

                if (total > MaxExtractedBytes)
                    throw new UserFacingException("The mod archive is too large to extract.");

                using Stream stream = entry.Open();
                using var buffer = new MemoryStream((int)Math.Min(entry.Length, int.MaxValue));
                var chunk = new byte[81920];
                long written = 0;
                int read;

                while ((read = stream.Read(chunk)) > 0)
                {
                    written += read;

                    if (written > entry.Length || total - entry.Length + written > MaxExtractedBytes)
                        throw new UserFacingException("The mod archive is too large to extract.");

                    buffer.Write(chunk, 0, read);
                }

                if (!files.TryAdd(name, buffer.ToArray()))
                    throw new UserFacingException("The mod archive lists the same file twice: " + name);
            }
        }
        catch (InvalidDataException)
        {
            throw new UserFacingException("The mod archive is damaged.");
        }

        return files;
    }

    [GeneratedRegex(@"^(BepInEx|Mods|Plugins|UserData|UserLibs)/", RegexOptions.IgnoreCase)]
    private static partial Regex RootedEntry();

    public static Dictionary<string, byte[]> ModEntries(Dictionary<string, byte[]> archive, ModLoader loader, string guid)
    {
        var rooted = archive.Keys.Any(name => RootedEntry().IsMatch(name));
        var entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        Regex allowed = loader == ModLoader.MelonLoader
            ? new Regex(@"^(Mods|Plugins|UserData|UserLibs)/", RegexOptions.IgnoreCase)
            : new Regex(@"^BepInEx/(plugins|patchers|config)/", RegexOptions.IgnoreCase);

        foreach ((var name, var data) in archive)
        {
            if (!rooted)
            {
                entries[(loader == ModLoader.MelonLoader ? "Mods/" : $"BepInEx/plugins/{guid}/") + name] = data;
                continue;
            }

            if (!allowed.IsMatch(name))
                throw new UserFacingException($"This mod's archive has files outside {loader.DisplayName()}'s mod folders, so it can't be installed with {loader.DisplayName()}.");

            var canonical = Regex.Replace(name, @"^(mods|plugins|userdata|userlibs)/", m => m.Groups[1].Value.ToLowerInvariant() switch
            {
                "mods" => "Mods/",
                "plugins" => "Plugins/",
                "userdata" => "UserData/",
                _ => "UserLibs/",
            }, RegexOptions.IgnoreCase);
            canonical = Regex.Replace(canonical, @"^bepinex/(plugins|patchers|config)/", m => "BepInEx/" + m.Groups[1].Value.ToLowerInvariant() + "/", RegexOptions.IgnoreCase);
            entries[canonical] = data;
        }

        return entries;
    }

    public static string SafeFileName(string name, string fallback)
    {
        var cleaned = Regex.Replace(name, @"[^A-Za-z0-9._ -]", "_").Trim(' ', '.');
        return cleaned.Length is > 0 and <= 120 ? cleaned : fallback;
    }
}
