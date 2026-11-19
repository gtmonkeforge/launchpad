using Launchpad.Infrastructure;

namespace Launchpad.Mods;

public static class ModFiles
{
    public static StringComparer PathComparer { get; } = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string Resolve(string game, string path)
    {
        if (!Path.IsPathRooted(path))
            return Archive.SafePath(game, path);

        var full = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(Path.GetFullPath(game), full);

        if (relative == "." || relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new UserFacingException("forge.json lists a file outside the game folder, so Launchpad won't touch it.");

        return full;
    }

    private static bool Within(string folder, string path)
    {
        var relative = Path.GetRelativePath(folder, path);
        return relative == "." || (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative));
    }

    private static IEnumerable<string> Walk(string game, string folder)
    {
        Archive.EnsureNoLinks(game, folder);

        if (!Directory.Exists(folder))
            yield break;

        foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
        {
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                throw new UserFacingException("A mod folder contains a symbolic link, so Launchpad left it alone.");

            if (Directory.Exists(entry))
            {
                foreach (var file in Walk(game, entry))
                    yield return file;
            }
            else
            {
                yield return entry;
            }
        }
    }

    public static async Task ApplyAsync(string game, Manifest manifest, InstalledMod record, IReadOnlyDictionary<string, byte[]> entries, bool removeOnly = false, bool adopt = false)
    {
        game = Path.GetFullPath(game);
        InstalledMod? old = manifest.Find(record.Guid);
        var targets = new Dictionary<string, byte[]>(PathComparer);

        foreach ((var name, var bytes) in entries)
            targets[Archive.SafePath(game, name)] = bytes;

        var oldPaths = new List<string>();

        foreach (var file in old?.Files ?? [])
            oldPaths.Add(Resolve(game, file));

        foreach (var folder in old?.Folders ?? [])
            oldPaths.AddRange(Walk(game, Resolve(game, folder)));

        List<InstalledMod> others = manifest.Installed.Where(mod => mod.Guid != record.Guid).ToList();
        var touched = new HashSet<string>(oldPaths.Concat(targets.Keys), PathComparer);
        var oldSet = new HashSet<string>(oldPaths, PathComparer);

        foreach (var target in touched)
        {
            Archive.EnsureNoLinks(game, target);

            foreach (InstalledMod other in others)
            {
                var shared = other.Files.Any(file => PathComparer.Equals(SafeResolve(game, file), target))
                    || other.Folders.Any(folder => SafeResolve(game, folder) is { } root && Within(root, target));

                if (shared)
                    throw new UserFacingException($"{Path.GetFileName(target)} also belongs to {other.Name}, so it can't be changed by {record.Name}.");
            }

            if (targets.ContainsKey(target) && File.Exists(target) && !oldSet.Contains(target) && !adopt)
                throw new UserFacingException($"Installing would overwrite {Path.GetRelativePath(game, target)}, which Launchpad didn't install. Move it out of the game folder, then try again.");
        }

        var backup = new Dictionary<string, byte[]?>(PathComparer);

        foreach (var target in touched)
            backup[target] = File.Exists(target) ? await File.ReadAllBytesAsync(target) : null;

        if (adopt)
        {
            foreach ((var target, var bytes) in backup)
            {
                if (bytes == null || oldSet.Contains(target))
                    continue;

                var copy = Archive.SafePath(game, $".launchpad/{record.Guid}-backup/{Path.GetRelativePath(game, target).Replace('\\', '/')}");
                Archive.EnsureNoLinks(game, copy);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);

                if (!File.Exists(copy))
                    await File.WriteAllBytesAsync(copy, bytes);
            }
        }

        try
        {
            foreach (var target in oldPaths)
                SafeFile.Delete(target);

            foreach ((var target, var bytes) in targets)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllBytesAsync(target, bytes);
            }

            manifest.Installed = others;

            if (!removeOnly)
            {
                record.Files = targets.Keys.ToList();
                record.Folders = [];
                manifest.Installed.Add(record);
            }

            await manifest.WriteAsync(game);
        }
        catch
        {
            foreach ((var target, var bytes) in backup)
            {
                try
                {
                    if (bytes != null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        await File.WriteAllBytesAsync(target, bytes);
                    }
                    else if (File.Exists(target))
                    {
                        File.Delete(target);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            if (old != null)
            {
                manifest.Installed = others;
                manifest.Installed.Add(old);
            }

            throw;
        }

        foreach (var removed in oldPaths.Where(path => !targets.ContainsKey(path)))
            PruneEmptyFolders(game, Path.GetDirectoryName(removed)!);
    }

    private static string? SafeResolve(string game, string path)
    {
        try
        {
            return Resolve(game, path);
        }
        catch (UserFacingException)
        {
            return null;
        }
    }

    private static void PruneEmptyFolders(string game, string folder)
    {
        var keep = new HashSet<string>(new[] { "", "BepInEx", "BepInEx/plugins", "BepInEx/patchers", "BepInEx/config", "Mods", "Plugins", "UserData", "UserLibs" }
            .Select(path => Path.GetFullPath(Path.Combine(game, path)).TrimEnd(Path.DirectorySeparatorChar)), PathComparer);

        var current = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);

        while (Within(game, current) && !keep.Contains(current))
        {
            try
            {
                if (!Directory.Exists(current) || Directory.EnumerateFileSystemEntries(current).Any())
                    return;

                Directory.Delete(current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return;
            }

            current = Path.GetDirectoryName(current)!;
        }
    }
}
