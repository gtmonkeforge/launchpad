namespace Launchpad.Ui;

public sealed partial class FilePicker
{
    private const int MaxEntries = 2000;

    private readonly List<Entry> _entries = [];
    private string _folder = "";

    private sealed record Entry(string Name, string Path, bool IsFolder, long Size);

    private static string FirstFolder(string? startAt)
    {
        foreach (var candidate in new[] { startAt, _lastFolder, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) })
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return Path.GetFullPath("/");
    }

    private void GoTo(string folder)
    {
        var entries = new List<Entry>();

        try
        {
            var info = new DirectoryInfo(folder);

            foreach (var directory in info.EnumerateDirectories().Where(d => !d.Name.StartsWith('.')).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                entries.Add(new Entry(directory.Name, directory.FullName, true, 0));

            if (!_foldersOnly)
            {
                foreach (var file in info.EnumerateFiles().Where(Wanted).OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                    entries.Add(new Entry(file.Name, file.FullName, false, file.Length));
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _problem = "Couldn't open that folder.";
            return;
        }

        _problem = "";
        _folder = folder;
        _typed = folder;
        _entries.Clear();
        _entries.AddRange(entries.Take(MaxEntries));
    }

    private bool Wanted(FileInfo file)
    {
        return !file.Name.StartsWith('.') && (_extensions.Length == 0 || _extensions.Contains(file.Extension.ToLowerInvariant()));
    }

    private void GoToTyped()
    {
        var path = _typed.Trim().Trim('"', '\'');

        if (path.StartsWith("~/", StringComparison.Ordinal))
            path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);

        if (Directory.Exists(path))
            GoTo(System.IO.Path.GetFullPath(path));
        else if (File.Exists(path) && !_foldersOnly)
        {
            GoTo(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
            Toggle(System.IO.Path.GetFullPath(path));
        }
        else
            _problem = "That path doesn't exist.";
    }
}
