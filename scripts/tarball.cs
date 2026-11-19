using System.Formats.Tar;
using System.IO.Compression;
using System.Text;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: dotnet run scripts/tarball.cs -- <source folder> <output .tar.gz> <folder name in archive>");
    return 1;
}

var source = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
var prefix = args[2].Trim('/');
string[] executables = ["Launchpad", "createdump", "install.sh", "uninstall.sh"];

const UnixFileMode Directory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
const UnixFileMode Executable = Directory;
const UnixFileMode Regular = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

using (var file = File.Create(output))
using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize))
using (var tar = new TarWriter(gzip, TarEntryFormat.Pax))
{
    tar.WriteEntry(new PaxTarEntry(TarEntryType.Directory, prefix + "/") { Mode = Directory });

    foreach (var directory in System.IO.Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
    {
        tar.WriteEntry(new PaxTarEntry(TarEntryType.Directory, $"{prefix}/{Relative(directory)}/") { Mode = Directory });
    }

    foreach (var path in System.IO.Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
    {
        var name = Relative(path);
        var executable = executables.Contains(name) || name.EndsWith(".so", StringComparison.Ordinal);
        var data = File.ReadAllBytes(path);

        if (name.EndsWith(".sh", StringComparison.Ordinal) || name.EndsWith(".desktop", StringComparison.Ordinal) || name.EndsWith(".txt", StringComparison.Ordinal))
            data = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(data).Replace("\r\n", "\n"));

        using var content = new MemoryStream(data);
        tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, $"{prefix}/{name}")
        {
            Mode = executable ? Executable : Regular,
            ModificationTime = File.GetLastWriteTimeUtc(path),
            DataStream = content,
        });
    }
}

Console.WriteLine($"Wrote {output}");
return 0;

string Relative(string path) => Path.GetRelativePath(source, path).Replace('\\', '/');
