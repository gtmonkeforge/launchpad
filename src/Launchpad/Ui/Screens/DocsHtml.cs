using System.Net;
using System.Text.RegularExpressions;

namespace Launchpad.Ui.Screens;

public static partial class DocsHtml
{
    [GeneratedRegex(@"<(script|style)\b.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scripts();

    [GeneratedRegex(@"<pre\b[^>]*>(.*?)</pre>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Pre();

    [GeneratedRegex(@"<h([1-6])\b[^>]*>(.*?)</h\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Heading();

    [GeneratedRegex(@"<a\b[^>]*?href=""([^""]*)""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Anchor();

    [GeneratedRegex(@"<(strong|b)\b[^>]*>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Bold();

    [GeneratedRegex(@"<code\b[^>]*>(.*?)</code>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Code();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"</(p|div|ul|ol|li|tr|table|blockquote|h[1-6])>|<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex Breaks();

    [GeneratedRegex(@"</t[dh]>", RegexOptions.IgnoreCase)]
    private static partial Regex Cells();

    [GeneratedRegex(@"<blockquote\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Quote();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex Blank();

    public static string ToMarkdown(string? html)
    {
        var text = Scripts().Replace(html ?? "", "");
        text = Pre().Replace(text, match => "\n\n```\n" + Tags().Replace(match.Groups[1].Value, "").Trim('\n') + "\n```\n\n");
        text = Heading().Replace(text, match => "\n\n" + new string('#', int.Parse(match.Groups[1].Value)) + " " + Tags().Replace(match.Groups[2].Value, "").Trim() + "\n\n");
        text = Code().Replace(text, match => "`" + Tags().Replace(match.Groups[1].Value, "") + "`");
        text = Anchor().Replace(text, match => "[" + Tags().Replace(match.Groups[2].Value, "").Trim() + "](" + match.Groups[1].Value + ")");
        text = Bold().Replace(text, match => "**" + match.Groups[2].Value + "**");
        text = ListItem().Replace(text, "\n- ");
        text = Quote().Replace(text, "\n> ");
        text = Cells().Replace(text, " | ");
        text = Breaks().Replace(text, "\n\n");
        text = Tags().Replace(text, "");
        text = WebUtility.HtmlDecode(text);
        return Blank().Replace(text, "\n\n").Trim();
    }
}
