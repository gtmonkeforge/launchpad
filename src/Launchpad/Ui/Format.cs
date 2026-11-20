using System.Globalization;
using System.Text.RegularExpressions;

namespace Launchpad.Ui;

public static partial class Format
{
    public static string Initials(string? name)
    {
        var words = (name ?? "?").Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2);
        var letters = string.Concat(words.Select(word => word.EnumerateRunes().First().ToString().ToUpperInvariant()));
        return letters.Length == 0 ? "?" : letters;
    }

    public static string Number(long value)
    {
        if (value >= 1_000_000)
            return (value / 1_000_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "M";

        if (value >= 1_000)
            return (value / 1_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "K";

        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static string Money(long cents, string currency)
    {
        var whole = cents / 100.0;
        return currency.ToLowerInvariant() == "usd" ? "$" + whole.ToString("0.00", CultureInfo.InvariantCulture) : whole.ToString("0.00", CultureInfo.InvariantCulture) + " " + currency.ToUpperInvariant();
    }

    public static string Date(string? text)
    {
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when) && when.Year >= 2000 ? when.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) : "";
    }

    public static string Ago(string? text, string fallback = "")
    {
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when) ? Ago(when) : fallback;
    }

    public static string Ago(DateTimeOffset when)
    {
        if (when.Year < 2000)
            return "a while ago";

        var secs = Math.Max(0, (DateTimeOffset.UtcNow - when).TotalSeconds);

        foreach (var (len, unit) in new[] { (31536000, "year"), (2592000, "month"), (86400, "day"), (3600, "hour"), (60, "minute") })
        {
            if (secs < len)
                continue;

            var n = (int)(secs / len);
            return n == 1 ? $"1 {unit} ago" : $"{n} {unit}s ago";
        }

        return "just now";
    }

    public static string PlainText(string? markdown)
    {
        var text = CodeFence().Replace(markdown ?? "", " ");
        text = MarkChars().Replace(text, "");
        text = ParenText().Replace(text, "");
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex("```[\\s\\S]*?```")]
    private static partial Regex CodeFence();

    [GeneratedRegex("[#*`>_~\\[\\]]")]
    private static partial Regex MarkChars();

    [GeneratedRegex("\\([^)]*\\)")]
    private static partial Regex ParenText();

    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespace();
}
