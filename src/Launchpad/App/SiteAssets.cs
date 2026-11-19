using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Launchpad.App;

public static partial class SiteAssets
{
    public sealed record Asset(byte[] Bytes, string ContentType);

    private static readonly ConcurrentDictionary<string, Asset?> Cache = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".json"] = "application/json",
        [".txt"] = "text/plain; charset=utf-8",
    };

    [GeneratedRegex(@"^/(static|_launchpad)/[A-Za-z0-9._/-]+$")]
    private static partial Regex AssetPath();

    [GeneratedRegex(@"\blocation\.(assign|replace)\(")]
    private static partial Regex ScriptedNavigation();

    public static byte[]? Read(string resource)
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("wwwroot/" + resource);

        if (stream == null)
            return null;

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public static string ReadText(string resource) => Encoding.UTF8.GetString(Read(resource) ?? throw new FileNotFoundException("Missing UI file: " + resource));

    public static Asset? Find(string path)
    {
        if (!AssetPath().IsMatch(path) || path.Contains("..", StringComparison.Ordinal))
            return null;

        return Cache.GetOrAdd(path, Load);
    }

    private static Asset? Load(string path)
    {
        var type = ContentTypes.GetValueOrDefault(Path.GetExtension(path), "application/octet-stream");

        if (path.StartsWith("/_launchpad/", StringComparison.Ordinal))
            return path.StartsWith("/_launchpad/overrides/", StringComparison.Ordinal) ? null : Wrap(Read("launchpad" + path["/_launchpad".Length..]), type);

        if (Read("launchpad/overrides" + path) is { } overridden)
            return new Asset(overridden, type);

        var original = path.EndsWith(".site.js", StringComparison.Ordinal) ? path[..^".site.js".Length] + ".js" : path;
        var bytes = Read("site" + original);

        if (bytes != null && type.StartsWith("text/javascript", StringComparison.Ordinal))
            bytes = Encoding.UTF8.GetBytes(ScriptedNavigation().Replace(Encoding.UTF8.GetString(bytes), match => match.Groups[1].Value == "assign" ? "__lpNavigate(" : "__lpNavigate.replace("));

        return Wrap(bytes, type);
    }

    private static Asset? Wrap(byte[]? bytes, string type) => bytes == null ? null : new Asset(bytes, type);

    public static string Shell(object boot)
    {
        var bootJson = JsonSerializer.Serialize(boot).Replace("<", "\\u003c");

        return ReadText("site/index.html")
            .Replace("{{brandClass}}", "")
            .Replace("{{title}}", "MonkeForge Launchpad")
            .Replace("{{description}}", "")
            .Replace("{{head}}", "")
            .Replace("</head>", "    <link rel=\"stylesheet\" href=\"/_launchpad/launchpad.css\">\n</head>")
            .Replace("{{brand}}", "MonkeForge")
            .Replace("{{boot}}", bootJson)
            .Replace("{{content}}", """<div class="page-loading" role="status"><span class="spinner" aria-hidden="true"></span>Loading…</div>""")
            .Replace("<body>", "<body>\n" + ReadText("launchpad/titlebar.html"))
            .Replace("<script type=\"module\" src=\"/static/js/main.js\"></script>",
                "<script type=\"module\" src=\"/_launchpad/launchpad.js\"></script>\n    <script type=\"module\" src=\"/static/js/main.js\"></script>");
    }
}
