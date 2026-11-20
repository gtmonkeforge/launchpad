using System.Globalization;
using System.Text.Json.Nodes;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

public sealed record ModSummary(
    string Id,
    string Guid,
    string Name,
    string Description,
    string Category,
    string? Status,
    string AuthorName,
    bool AuthorIsAdmin,
    bool AuthorIsVerified,
    string[] Badges,
    long Downloads,
    DateTimeOffset UpdatedAt,
    string Version,
    string? Logo,
    string? Price,
    string AuthorId = "")
{
    public static ModSummary From(JsonNode mod, string? myId)
    {
        JsonNode? author = mod["author"];
        var guid = mod.Str("guid") ?? "";
        var gated = mod.Bool("gated");
        var priceCents = mod.Long("price_cents");
        var currency = mod.Str("currency") ?? "usd";
        string? price = null;

        if (gated)
        {
            if (mod.Bool("owned") && author.Str("id") != myId)
                price = "Owned";
            else if (priceCents > 0)
                price = Format.Money(priceCents, currency);
            else
                price = $"From {Format.Money(mod.Long("min_tier_cents"), currency)}/mo";
        }

        var badges = mod["badges"] is JsonArray list ? list.Select(badge => badge?.GetValue<string>() ?? "").ToArray() : [];
        DateTimeOffset.TryParse(mod.Str("updated_at"), CultureInfo.InvariantCulture, out var updated);

        return new ModSummary(
            mod.Str("id") ?? guid,
            guid,
            mod.Str("name") ?? guid,
            Format.PlainText(mod.Str("description")),
            mod.Str("category") ?? "",
            mod.Str("status"),
            author.Str("name") ?? "",
            author.Bool("is_admin"),
            author.Bool("is_verified"),
            badges,
            mod.Long("downloads"),
            updated,
            mod.Str("current_version") ?? "",
            mod.Str("logo"),
            price,
            author.Str("id") ?? "");
    }
}
