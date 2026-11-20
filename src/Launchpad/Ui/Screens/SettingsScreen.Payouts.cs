using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private double DrawPayouts(Ui ui, double left, double width)
    {
        if (Waiting(ui, left, width, out var bottom))
            return bottom;

        var fee = _data.Long("fee_percent");
        var connected = _data.Bool("connected");
        var charges = _data.Bool("charges_enabled");
        var (stateText, stateColor) = charges && !_data.Bool("can_sell") ? ("Paused", Palette.Bad) : charges ? ("Ready to sell", Palette.Good) : connected ? ("Setup incomplete", Palette.Warn) : ("Not connected", Palette.Muted);
        var intro = "Connect a Stripe account to sell mods and receive donations. Stripe handles your bank and identity details; MonkeForge never sees them.";
        var fees = $"On sales, MonkeForge keeps {fee}% and Stripe's processing fee comes out of your share. Donations reach you in full.";
        var height = 62 + Card.HintHeight(intro, width) + Card.HintHeight(fees, width) + 62;
        Card card = Card.Open(ui, "payouts-panel", left, TopMargin, width, height, "Get paid");
        card.PillRight(width - Card.Padding, 17, stateText, stateColor);
        var top = 44 + card.Hint(0, 44, width, intro) + 4;
        top += card.Hint(0, top, width, fees) + 10;

        if (!charges && Widgets.Button(ui, "payouts-connect", left + Card.Padding, TopMargin + top, connected ? "Finish Stripe setup" : "Connect with Stripe", ButtonKind.Primary, out _, true, 36))
            shell.OpenForge("/settings/payouts");

        if (connected && Widgets.Button(ui, "payouts-dashboard", left + Card.Padding + (charges ? 0 : 180), TopMargin + top, "Open Stripe dashboard", ButtonKind.Subtle, out _, true, 36))
            shell.Call("shell.open", new JsonObject { ["url"] = "https://dashboard.stripe.com" });

        return connected ? DrawTotals(ui, left, TopMargin + height + 16, width) : TopMargin + height;
    }

    private double DrawTotals(Ui ui, double left, double y, double width)
    {
        var currency = _data.Str("currency") ?? "usd";
        JsonNode? totals = _data?["totals"];
        var cells = new (string Label, long Cents)[]
        {
            ("Sales", totals.Long("sales_cents")),
            ("MonkeForge fee", totals.Long("fees_cents")),
            ("Your earnings", totals.Long("earnings_cents")),
            ("Donations", totals.Long("donations_cents")),
        };

        Card strip = Card.Open(ui, "payouts-totals", left, y, width, 70, "");
        var cell = width / cells.Length;

        for (var i = 0; i < cells.Length; i++)
        {
            strip.Text(i * cell + 20, 24, 13, cells[i].Label, Palette.Muted);
            strip.Text(i * cell + 20, 48, 16, Format.Money(cells[i].Cents, currency), Palette.Text, true);
        }

        return DrawRecent(ui, left, y + 86, width);
    }

    private double DrawRecent(Ui ui, double left, double y, double width)
    {
        var items = (_data?["recent"] as JsonArray)?.Where(item => item != null).Select(item => item!).ToList() ?? [];
        var height = 56 + Math.Max(1, items.Count) * 56;
        Card card = Card.Open(ui, "payouts-recent", left, y, width, height, "Recent activity");

        if (items.Count == 0)
            card.Hint(0, 44, width, "No sales or donations yet.");

        for (var i = 0; i < items.Count; i++)
        {
            var kind = items[i].Str("kind") switch { "purchase" => "Sale", "subscription" => "Membership", _ => "Donation" };
            var name = items[i].Str("name") is { Length: > 0 } text ? kind + " · " + text : kind;
            var earned = items[i].Long("amount_cents") - items[i].Long("fee_cents");
            var top = 52 + i * 56;
            card.Bold(Card.Padding, top + 14, 14.5, Chrome.Clip(Fonts.Main, 14.5, name, width - 220), Palette.Text);
            var when = items[i].Str("created_at") is { } created && DateTimeOffset.TryParse(created, out var date) ? Format.Ago(date) : "";
            card.Text(Card.Padding, top + 36, 13, when, Palette.Muted);
            var money = Format.Money(earned, items[i].Str("currency") ?? "usd");
            card.Text(width - Card.Padding - Fonts.Mono.Measure(14, money), top + 24, 14, money, Palette.Text, true);
        }

        return y + height;
    }
}
