using System.Globalization;
using System.Text.Json.Nodes;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

public sealed record ModSummary
{
    public string Id { get; init; } = "";
    public string Guid { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    public string? Status { get; init; }
    public string AuthorName { get; init; } = "";
    public bool AuthorIsAdmin { get; init; }
    public bool AuthorIsVerified { get; init; }
    public string[] Badges { get; init; } = [];
    public long Downloads { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string Version { get; init; } = "";
    public string? Logo { get; init; }
    public string? Price { get; init; }
    public string AuthorId { get; init; } = "";

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

        return new ModSummary
        {
            Id = mod.Str("id") ?? guid,
            Guid = guid,
            Name = mod.Str("name") ?? guid,
            Description = Format.PlainText(mod.Str("description")),
            Category = mod.Str("category") ?? "",
            Status = mod.Str("status"),
            AuthorName = author.Str("name") ?? "",
            AuthorIsAdmin = author.Bool("is_admin"),
            AuthorIsVerified = author.Bool("is_verified"),
            Badges = badges,
            Downloads = mod.Long("downloads"),
            UpdatedAt = updated,
            Version = mod.Str("current_version") ?? "",
            Logo = mod.Str("logo"),
            Price = price,
            AuthorId = author.Str("id") ?? "",
        };
    }
}
