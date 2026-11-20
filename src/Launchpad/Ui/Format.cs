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

    public static string Ago(DateTimeOffset when)
    {
        var span = DateTimeOffset.UtcNow - when;
        var seconds = span.TotalSeconds;

        if (seconds < 60)
            return "just now";

        if (seconds < 3600)
            return Plural((int)(seconds / 60), "minute");

        if (seconds < 86400)
            return Plural((int)(seconds / 3600), "hour");

        if (seconds < 86400 * 30)
            return Plural((int)(seconds / 86400), "day");

        if (seconds < 86400 * 365)
            return Plural((int)(seconds / (86400 * 30)), "month");

        return Plural((int)(seconds / (86400 * 365)), "year");
    }

    private static string Plural(int amount, string unit) => amount == 1 ? $"1 {unit} ago" : $"{amount} {unit}s ago";

    public static string PlainText(string? markdown)
    {
        var text = Fence().Replace(markdown ?? "", " ");
        text = Marks().Replace(text, "");
        text = Parens().Replace(text, "");
        return Spaces().Replace(text, " ").Trim();
    }

    [GeneratedRegex("```[\\s\\S]*?```")]
    private static partial Regex Fence();

    [GeneratedRegex("[#*`>_~\\[\\]]")]
    private static partial Regex Marks();

    [GeneratedRegex("\\([^)]*\\)")]
    private static partial Regex Parens();

    [GeneratedRegex("\\s+")]
    private static partial Regex Spaces();
}
