using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private static readonly Dictionary<string, string> StatusNames = new() { ["refunded"] = "Refunded", ["disputed"] = "Disputed", ["failed"] = "Failed" };

    private string? _payoutFor;
    private List<JsonNode> _ownedGroups = [];

    private async Task<JsonNode?> FetchPayouts()
    {
        var forge = shell.App.Forge;

        try
        {
            JsonNode groups = await forge.GetJsonAsync("/api/v1/groups");
            _ownedGroups = ListOf(groups, "items").Where(group => group.Str("role") == "owner").ToList();
        }
        catch (UserFacingException)
        {
            _ownedGroups = [];
        }

        return await forge.GetJsonAsync("/api/v1/payouts" + PayoutQuery());
    }

    private string PayoutQuery() => _payoutFor == null ? "" : "?account=" + Uri.EscapeDataString(_payoutFor);

    private double DrawPayouts(Ui ui, double left, double width)
    {
        if (Waiting(ui, left, width, out var bottom))
            return bottom;

        var fee = _data.Long("fee_percent");
        var connected = _data.Bool("connected");
        var charges = _data.Bool("charges_enabled");
        var (stateText, stateColor) = charges && !_data.Bool("can_sell") ? ("Sales paused", Palette.Bad) : charges ? ("Ready to sell", Palette.Good) : connected ? ("Setup incomplete", Palette.Warn) : ("Not connected", Palette.Muted);
        var intro = "Connect a Stripe account to sell mods and receive donations. Stripe handles your bank and identity details; MonkeForge never sees them.";
        var fees = $"On sales, MonkeForge keeps {fee}% and Stripe's processing fee comes out of your share. Donations reach you in full.";
        var y = TopMargin;
        y = DrawAccountPicker(ui, left, y, width);
        var height = 62 + Card.HintHeight(intro, width) + Card.HintHeight(fees, width) + 62;
        Card card = Card.Open(ui, "payouts-panel", left, y, width, height, "Get paid");
        card.PillRight(width - Card.Padding, 17, stateText, stateColor);
        var top = 44 + card.Hint(0, 44, width, intro) + 4;
        top += card.Hint(0, top, width, fees) + 10;

        if (!_data.Bool("enabled"))
            card.Text(Card.Padding, top + 18, 14, "Payments aren't set up on this server yet.", Palette.Warn);
        else
            DrawPayoutButtons(ui, left, y + top, connected, charges);

        y += height + 16;

        if (connected && _data?["privacy"] is JsonNode privacy)
            y = DrawPrivacy(ui, left, y, width, privacy) + 16;

        return connected ? DrawTotals(ui, left, y, width) : y - 16;
    }

    private double DrawAccountPicker(Ui ui, double left, double y, double width)
    {
        if (_ownedGroups.Count == 0)
            return y;

        var names = new List<string> { "You" };
        names.AddRange(_ownedGroups.Select(group => (group.Str("name") ?? "Group") + " (group)"));
        var chosen = _payoutFor == null ? 0 : Math.Max(0, _ownedGroups.FindIndex(group => group.Str("id") == _payoutFor) + 1);
        Card card = Card.Open(ui, "payouts-for", left, y, width, 72, "");
        card.Text(Card.Padding, 36, 14, "Payouts for", Palette.Muted);
        ui.Pos(left + Card.Padding + 110, y + 18);
        ui.Size(Math.Min(300, width - 150), 36);

        if (ui.Combo("##payouts-account", ref chosen, names))
        {
            _payoutFor = chosen == 0 ? null : _ownedGroups[chosen - 1].Str("id");
            _loadedSection = -1;
        }

        return y + 72 + 16;
    }

    private void DrawPayoutButtons(Ui ui, double left, double y, bool connected, bool charges)
    {
        var x = left + Card.Padding;

        if (!charges)
        {
            if (Widgets.Button(ui, "payouts-connect", x, y, connected ? "Finish Stripe setup" : "Connect with Stripe", ButtonKind.Primary, out var width, true, 36, Glyphs.Link))
                StartOnboarding();

            x += width + 10;
        }

        if (connected && Widgets.Button(ui, "payouts-dashboard", x, y, "Open Stripe dashboard", ButtonKind.Subtle, out _, true, 36, Glyphs.Link))
            shell.Call("shell.open", new JsonObject { ["url"] = "https://dashboard.stripe.com" });
    }

    private void StartOnboarding()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode result = await shell.App.Forge.SendJsonAsync(HttpMethod.Post, "/api/v1/payouts/onboard" + PayoutQuery(), null);

                if (result.Str("url") is { Length: > 0 } url)
                    shell.Call("shell.open", new JsonObject { ["url"] = url });
            }
            catch (UserFacingException ex)
            {
                shell.Toast(ex.Message);
            }
        });
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
            card.Text(width - Card.Padding - Fonts.Mono.Measure(14, money) - (items[i].Str("status") is "paid" or null ? 0 : 110), top + 24, 14, money, Palette.Text, true);

            if (items[i].Str("status") is { } status && status != "paid")
                card.PillRight(width - Card.Padding, top + 13, StatusNames.GetValueOrDefault(status, status), Palette.Bad);
        }

        return y + height;
    }
}
