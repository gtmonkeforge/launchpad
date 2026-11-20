using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private const double TierGap = 14;
    private const double TierPad = 20;

    private double DrawTiers(Ui ui, double left, double width, double y)
    {
        Item note = Area(ui, "mod-tiers-intro", left, y, width, 24);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 14, note.X, note.Y + 12, "Pick a monthly tier to unlock this mod. You can cancel any time from your profile.", Palette.Muted);
        y += 38;

        if (Locked && _mod.Long("price_cents") > 0)
            y = DrawOneTime(ui, left, width, y) + TierGap;

        var columns = width >= 560 ? 2 : 1;
        var cardWidth = (width - TierGap * (columns - 1)) / columns;

        for (var i = 0; i < Tiers.Count; i += columns)
        {
            var row = Tiers.Skip(i).Take(columns).ToList();
            var height = row.Max(tier => TierHeight(tier, cardWidth));

            for (var j = 0; j < row.Count; j++)
                DrawTier(ui, row[j], i + j, left + j * (cardWidth + TierGap), y, cardWidth, height);

            y += height + TierGap;
        }

        return y - TierGap;
    }

    private double DrawOneTime(Ui ui, double left, double width, double y)
    {
        Item card = Area(ui, "mod-once", left, y, width, 84);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, 84, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 17, card.X + TierPad, card.Y + 30, "One-time purchase", Palette.Text);
        Chrome.Text(c, Fonts.Main, 14, card.X + TierPad, card.Y + 58, "Buy once to install every future update.", Palette.Muted);
        var option = PurchaseOptions().First(item => item.Value == "once");
        var buttonWidth = Math.Max(150, Fonts.Main.Measure(14, option.Button) + 40);

        if (!_shell.SignedIn)
        {
            if (Button(ui, "mod-once-buy", "Sign in to buy", left + width - TierPad - 150, y + 22, 150, 38, ButtonStyle.Primary))
                _shell.Router.Push(new Route(Page.SignIn));
        }
        else if (Button(ui, "mod-once-buy", option.Button, left + width - TierPad - buttonWidth, y + 22, buttonWidth, 38, ButtonStyle.Primary, !_buying))
            StartPurchase(option);

        return y + 84;
    }

    private static List<string> BenefitLines(JsonNode? tier, double width)
    {
        var lines = new List<string>();

        foreach (var benefit in tier?["benefits"] as JsonArray ?? [])
            lines.AddRange(Chrome.Wrap(Fonts.Main, 14, benefit?.GetValue<string>() ?? "", width - TierPad * 2 - 24, 3));

        return lines;
    }

    private static string ModsLine(JsonNode? tier)
    {
        var names = (tier?["mods"] as JsonArray)?.Select(item => item.Str("name") ?? "").ToList();
        return names is { Count: > 0 } ? "Mods included: " + string.Join(", ", names) : "";
    }

    private double TierHeight(JsonNode? tier, double width)
    {
        var lines = BenefitLines(tier, width).Count;
        var extra = ModsLine(tier).Length > 0 ? WrapLines(Fonts.Main, 13.5, ModsLine(tier), width - TierPad * 2, 3).Count * 20 + 10 : 0;
        var creator = tier.Str("scope") == "creator" ? 30 : 0;
        return TierPad + creator + 34 + 36 + lines * 22 + extra + 62;
    }

    private void DrawTier(Ui ui, JsonNode? tier, int index, double x, double y, double width, double height)
    {
        Item card = Area(ui, "mod-tier-" + index, x, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        var top = card.Y + TierPad;

        if (tier.Str("scope") == "creator")
        {
            Widgets.Pill(c, card.X + TierPad, top, "Membership by " + tier.Str("owner_name"), Palette.Muted);
            top += 30;
        }

        Chrome.BoldText(c, Fonts.Main, 17, card.X + TierPad, top + 14, tier.Str("name") ?? "", Palette.Text);
        var price = Format.Money(tier.Long("price_cents"), tier.Str("currency") ?? Currency) + "/month";
        Chrome.BoldText(c, Fonts.Mono, 19, card.X + TierPad, top + 46, price, Palette.Accent);
        top += 74;

        foreach (var line in BenefitLines(tier, width))
        {
            Icons.Check(c, card.X + TierPad + 7, top + 10, Palette.Good);
            Chrome.Text(c, Fonts.Main, 14, card.X + TierPad + 24, top + 10, line, Palette.Text);
            top += 22;
        }

        var mods = ModsLine(tier);

        if (mods.Length > 0)
            foreach (var line in WrapLines(Fonts.Main, 13.5, mods, width - TierPad * 2, 3))
            {
                Chrome.Text(c, Fonts.Main, 13.5, card.X + TierPad, top + 16, line, Palette.Muted);
                top += 20;
            }

        DrawTierAction(ui, tier, index, x + TierPad, y + height - 52, width - TierPad * 2);
    }

    private void DrawTierAction(Ui ui, JsonNode? tier, int index, double x, double y, double width)
    {
        if (tier.Bool("subscribed"))
        {
            Item chip = Area(ui, "mod-tier-sub-" + index, x, y + 6, 120, 30);
            Canvas c = ui.Canvas()!;
            Icons.Check(c, chip.X + 8, chip.Y + 15, Palette.Good);
            Chrome.BoldText(c, Fonts.Main, 14, chip.X + 24, chip.Y + 15, "Subscribed", Palette.Good);
            return;
        }

        if (_owner)
            return;

        if (!_shell.SignedIn)
        {
            if (Button(ui, "mod-tier-go-" + index, "Subscribe", x, y, width, 38, ButtonStyle.Primary))
                _shell.Router.Push(new Route(Page.SignIn));

            return;
        }

        var option = PurchaseOptions().FirstOrDefault(item => item.Value == "tier:" + tier.Str("id"));

        if (option != null && Button(ui, "mod-tier-go-" + index, "Subscribe", x, y, width, 38, ButtonStyle.Primary, !_buying))
            StartPurchase(option);
    }
}
