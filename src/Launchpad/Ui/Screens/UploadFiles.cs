using System.Net.Http.Headers;

namespace Launchpad.Ui.Screens;

public static class UploadFiles
{
    public const long Megabyte = 1024 * 1024;

    public static string? Check(string raw, long maxBytes, string[] extensions, string tooBig, string wrongType)
    {
        var path = raw.Trim();

        if (path.Length == 0)
            return null;

        if (!File.Exists(path))
            return "Couldn't find the file " + path;

        if (!extensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            return wrongType;

        return new FileInfo(path).Length > maxBytes ? tooBig : null;
    }

    public static void Add(MultipartFormDataContent form, string field, string raw)
    {
        var path = raw.Trim();

        if (path.Length == 0)
            return;

        var content = new ByteArrayContent(File.ReadAllBytes(path));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, field, Path.GetFileName(path));
    }

    public static string Size(long bytes)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        return bytes >= Megabyte ? (bytes / (double)Megabyte).ToString("0.0", invariant) + " MB" : Math.Max(1, bytes / 1024) + " KB";
    }
}
