using System.Globalization;
using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ProfileScreen
{
    private double DrawPurchases(Ui ui, double left, double width, double y)
    {
        ui.Pos(left, y);
        ui.Custom("profile-private", width, 24, ItemFlags.NoHit, out Item note);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 14.5, note.X, note.Y + 12, "Only you can see this.", Palette.Muted);
        y += 38;

        if (_purchaseError != null)
            return DrawEmpty(ui, "profile-purchase-error", left, y, width, "Couldn't load your purchases", _purchaseError, null);

        if (_purchases.Count == 0 && _subscriptions.Count == 0)
            return DrawEmpty(ui, "profile-no-purchases", left, y, width, "Nothing here yet", "Mods and memberships you pay for will show up here.", null);

        y = Heading(ui, "Subscriptions", _subscriptions.Count, left, width, y);

        foreach (JsonNode item in _subscriptions)
            y = DrawSubscription(ui, item, left, width, y) + 8;

        y = Heading(ui, "One-time purchases", _purchases.Count, left, width, y);

        foreach (JsonNode item in _purchases)
            y = DrawPurchase(ui, item, left, width, y) + 8;

        return y;
    }

    private static double Heading(Ui ui, string title, int count, double left, double width, double y)
    {
        if (count == 0)
            return y;

        ui.Pos(left, y);
        ui.Custom("profile-heading-" + title, width, 30, ItemFlags.NoHit, out Item head);
        Chrome.BoldText(ui.Canvas()!, Fonts.Main, 17, head.X, head.Y + 15, title, Palette.Text);
        return y + 40;
    }

    private double DrawSubscription(Ui ui, JsonNode item, double left, double width, double y)
    {
        var id = item.Str("id") ?? "";
        var creator = item.Str("scope") == "creator";
        var name = creator ? $"{item.Str("owner_name")} · {item.Str("tier_name")}" : $"{item.Str("mod_name") ?? item.Str("guid")} · {item.Str("tier_name")}";
        var live = item.Bool("live");
        var ends = item.Bool("cancel_at_period_end");
        var when = DateTimeOffset.TryParse(item.Str("current_period_end"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) : "";
        var state = !live ? "Ended" : item.Str("status") == "past_due" ? "Payment failed. Update your card in the email from Stripe." : ends ? "Ends " + when : when.Length > 0 ? "Renews " + when : "";
        var line = Format.Money(item.Long("price_cents"), item.Str("currency") ?? "usd") + "/month" + (state.Length > 0 ? " · " + state : "");
        var note = _staffNotes.GetValueOrDefault(id);
        var height = note == null ? 70.0 : 96;

        ui.Pos(left, y);
        ui.Custom("profile-sub-" + id, width, height, ItemFlags.NoHit, out Item row);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, row.X, row.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 15.5, row.X + 20, row.Y + 25, Chrome.Clip(Fonts.Main, 15.5, name, width - 340), Palette.Text);
        Chrome.Text(c, Fonts.Main, 13.5, row.X + 20, row.Y + 49, Chrome.Clip(Fonts.Main, 13.5, line, width - 340), Palette.Muted);

        if (note != null)
            Chrome.Text(c, Fonts.Main, 13.5, row.X + 20, row.Y + 75, Chrome.Clip(Fonts.Main, 13.5, "Staff reply: " + note, width - 40), Palette.Warn);

        var right = left + width - 16;

        if (live && !ends)
        {
            var confirming = _confirmCancel == id;
            var label = confirming ? "Click again to confirm" : "Unsubscribe";
            right -= Widgets.ButtonWidth(label);

            if (Widgets.Button(ui, "profile-unsub-" + id, right, y + 17, label, confirming ? ButtonKind.Danger : ButtonKind.Subtle, out _, !_busy.ContainsKey("sub-" + id)))
            {
                if (confirming)
                    Unsubscribe(id);

                _confirmCancel = confirming ? null : id;
            }

            right -= 8;
        }

        if (item.Str("dispute_status") is { Length: > 0 } dispute)
            Widgets.Pill(ui.Canvas()!, row.X + width - 16 - (left + width - 16 - right) - Fonts.Main.Measure(12.5, "Dispute " + dispute) - 18, row.Y + 24, "Dispute " + dispute, Palette.Warn);
        else if (item.Bool("can_dispute") && Widgets.Button(ui, "profile-report-" + id, right - Widgets.ButtonWidth("Report a problem"), y + 17, "Report a problem", ButtonKind.Subtle, out _))
            shell.OpenForge((_profile.Str("profile_url") ?? "/profile") + "#purchases");

        return y + height;
    }

    private double DrawPurchase(Ui ui, JsonNode item, double left, double width, double y)
    {
        var guid = item.Str("guid") ?? "";
        var paymentId = item.Str("payment_id") ?? guid;
        var name = item.Str("name") ?? guid;
        var available = item.Bool("available");
        var note = _staffNotes.GetValueOrDefault(paymentId);
        var height = note == null ? 78.0 : 104;

        ui.Pos(left, y);
        ui.Custom("profile-buy-" + paymentId, width, height, ItemFlags.NoHit, out Item row);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, row.X, row.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Widgets.Tile(c, shell, row.X + 15, row.Y + 15, 46, name, item.Str("logo"));

        if (available)
        {
            Item link = Chrome.Hit(ui, "profile-buy-name-" + paymentId, left + 76, y + 11, Fonts.Main.Measure(16, name), 24);
            Chrome.BoldText(ui.Canvas()!, Fonts.Main, 16, link.X, link.Y + 12, name, link.Hovered ? Palette.Accent : Palette.Text);

            if (link.Clicked)
                shell.Router.Push(new Route(Page.Mod, guid));
        }
        else
            Chrome.BoldText(c, Fonts.Main, 16, row.X + 76, row.Y + 23, name, Palette.Text);

        c = ui.Canvas()!;
        var x = row.X + 76;

        if (item.Long("amount_cents") > 0)
            x += Widgets.Pill(c, x, row.Y + 42, Format.Money(item.Long("amount_cents"), item.Str("currency") ?? "usd"), Palette.Muted, true) + 5;

        if (DateTimeOffset.TryParse(item.Str("purchased_at"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
            x += Widgets.Pill(c, x, row.Y + 42, when.ToString("MMM d, yyyy", CultureInfo.InvariantCulture), Palette.Muted) + 5;

        if (!available)
            Widgets.Pill(c, x, row.Y + 42, "No longer listed", Palette.Bad);

        if (note != null)
            Chrome.Text(c, Fonts.Main, 13.5, row.X + 76, row.Y + 90, Chrome.Clip(Fonts.Main, 13.5, "Staff reply: " + note, width - 100), Palette.Warn);

        var right = left + width - 16;

        if (item.Str("dispute_status") is { Length: > 0 } dispute)
            Widgets.Pill(c, row.X + width - 16 - Fonts.Main.Measure(12.5, "Dispute " + dispute) - 18, row.Y + 28, "Dispute " + dispute, Palette.Warn);
        else if (item.Bool("can_dispute") && Widgets.Button(ui, "profile-report-" + paymentId, right - Widgets.ButtonWidth("Report a problem"), y + 22, "Report a problem", ButtonKind.Subtle, out _))
            shell.OpenForge((_profile.Str("profile_url") ?? "/profile") + "#purchases");

        return y + height;
    }
}
