using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private static readonly (string Key, string Label)[] PrivacyFields =
    [
        ("name", "Public business name"),
        ("support_email", "Support email"),
        ("support_phone", "Support phone"),
        ("support_address", "Support address"),
        ("statement_descriptor", "Statement descriptor"),
    ];

    private double DrawPrivacy(Ui ui, double left, double y, double width, JsonNode privacy)
    {
        var wrong = (privacy["issues"] as JsonArray)?.Select(i => i?.GetValue<string>()).ToHashSet() ?? [];
        var grace = privacy.Str("grace_ends_at");
        var notice = wrong.Count == 0 ? "" : grace != null
            ? "Some of your public Stripe details show buyers your personal information. Change the fields below in Stripe to match before " + Format.Date(grace) + ", or paid sales and membership billing will pause until you do."
            : "Some of your public Stripe details would show buyers your personal information, so paid sales and membership billing are paused. Change the fields below in Stripe to match, then refresh this page.";
        var noticeHeight = notice.Length == 0 ? 0 : Card.HintHeight(notice, width) + 8;
        var intro = "Buyers see these on receipts and bank statements. MonkeForge fills them in with its own contact details so your personal ones stay private.";
        var height = 56 + Card.HintHeight(intro, width) + noticeHeight + PrivacyFields.Length * 40 + 60;
        Card card = Card.Open(ui, "payouts-privacy", left, y, width, height, "Your public Stripe details");
        var top = 44 + card.Hint(0, 44, width, intro) + 4;

        if (notice.Length > 0)
            top += card.Hint(0, top, width, notice, Palette.Warn) + 8;

        foreach (var (key, label) in PrivacyFields)
        {
            card.Bold(Card.Padding, top + 12, 13.5, label, Palette.Text);
            card.Text(Card.Padding + 200, top + 12, 13, Chrome.Clip(Fonts.Mono, 13, privacy["expected"].Str(key) ?? "", width - 200 - 150), Palette.Muted, true);

            if (wrong.Contains(key))
                card.PillRight(width - Card.Padding, top + 1, "Needs changing", Palette.Bad);

            top += 40;
        }

        if (Widgets.Button(ui, "privacy-edit", left + Card.Padding, y + top + 8, "Edit public details in Stripe", wrong.Count > 0 ? ButtonKind.Primary : ButtonKind.Subtle, out var buttonWidth, true, 36, Glyphs.Link))
            shell.Call("shell.open", new JsonObject { ["url"] = "https://dashboard.stripe.com/settings/public" });

        if (Widgets.Button(ui, "privacy-guide", left + Card.Padding + buttonWidth + 10, y + top + 8, "How this works", ButtonKind.Subtle, out _, true, 36))
            shell.OpenForge("/docs/creators/payouts#keeping-your-details-private");

        return y + height;
    }
}
