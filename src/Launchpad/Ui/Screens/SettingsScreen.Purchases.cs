using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private const double RowHeight = 64;

    private bool _reasonOpen;
    private bool _cancelOpen;
    private string _reason = "";
    private string? _disputePayment;
    private string? _disputeSubscription;
    private string _targetName = "";
    private string? _cancelId;

    private async Task<JsonNode?> FetchPurchases()
    {
        var forge = shell.App.Forge;
        var bought = forge.GetJsonAsync("/api/v1/purchases");
        var subscriptions = forge.GetJsonAsync("/api/v1/subscriptions");
        var disputes = forge.GetJsonAsync("/api/v1/disputes");
        await Task.WhenAll(bought, subscriptions, disputes);

        return new JsonObject
        {
            ["purchases"] = bought.Result["items"]?.DeepClone() ?? new JsonArray(),
            ["subscriptions"] = subscriptions.Result["items"]?.DeepClone() ?? new JsonArray(),
            ["disputes"] = disputes.Result["items"]?.DeepClone() ?? new JsonArray(),
        };
    }

    private static List<JsonNode> ListOf(JsonNode? data, string key)
    {
        return (data?[key] as JsonArray)?.Where(item => item != null).Select(item => item!).ToList() ?? [];
    }

    private double DrawPurchases(Ui ui, double left, double width)
    {
        if (Waiting(ui, left, width, out var bottom))
            return bottom;

        var subscriptions = ListOf(_data, "subscriptions");
        var bought = ListOf(_data, "purchases");
        var notes = ListOf(_data, "disputes").ToDictionary(d => d.Str("payment_id") ?? d.Str("subscription_id") ?? "", d => d.Str("staff_note") ?? "", StringComparer.Ordinal);
        var y = TopMargin;
        y = DrawReceipts(ui, left, y, width);

        if (subscriptions.Count == 0 && bought.Count == 0)
            return DrawNothingBought(ui, left, y + 16, width);

        if (subscriptions.Count > 0)
            y = DrawSubscriptions(ui, left, y + 16, width, subscriptions, notes);

        if (bought.Count > 0)
            y = DrawBought(ui, left, y + 16, width, bought, notes);

        return y;
    }

    private double DrawReceipts(Ui ui, double left, double y, double width)
    {
        var hint = "Stripe emails you a receipt for every payment. Card details and refunds are handled on monkeforge.org.";
        var height = 118 + Card.HintHeight(hint, width);
        Card card = Card.Open(ui, "purchases-receipts-card", left, y, width, height, "Receipts and disputes");
        card.Hint(0, 44, width, hint);
        var top = y + height - 56;

        if (Widgets.Button(ui, "purchases-library", left + Card.Padding, top, "Open Library", ButtonKind.Primary, out var buttonWidth, true, 36))
            shell.Router.Push(new Route(Page.Library));

        if (Widgets.Button(ui, "purchases-receipts", left + Card.Padding + buttonWidth + 10, top, "Receipts on monkeforge.org", ButtonKind.Subtle, out _, true, 36))
            shell.OpenForge((shell.User.Str("profile_url") ?? "/settings") + "#purchases");

        return y + height;
    }

    private double DrawNothingBought(Ui ui, double left, double y, double width)
    {
        Card card = Card.Open(ui, "purchases-empty", left, y, width, 90, "Your purchases");
        card.Hint(0, 44, width, "Mods you buy and memberships you join will show up here.");
        return y + 90;
    }

    private double DrawSubscriptions(Ui ui, double left, double y, double width, List<JsonNode> items, Dictionary<string, string> notes)
    {
        var height = 56 + items.Count * RowHeight;
        Card card = Card.Open(ui, "purchases-subs", left, y, width, height, "Memberships");

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var top = 52 + i * RowHeight;
            var name = item.Str("scope") == "creator" ? item.Str("owner_name") + " · " + item.Str("tier_name") : (item.Str("mod_name") ?? item.Str("guid")) + " · " + item.Str("tier_name");
            var live = item.Bool("live");
            var when = Format.Date(item.Str("current_period_end"));
            var state = !live ? "Ended" : item.Str("status") == "past_due" ? "Payment failed. Update your card in the email from Stripe." : item.Bool("cancel_at_period_end") ? "Ends " + when : when.Length > 0 ? "Renews " + when : "";
            var price = Format.Money(item.Long("price_cents"), item.Str("currency") ?? "usd") + " a month";
            Row(card, top, width, name, Joined(price, state, Reply(notes, item.Str("id"))), item.Str("dispute_status"));
        }

        for (var i = 0; i < items.Count; i++)
            DrawSubscriptionButtons(ui, items[i], left + width - Card.Padding, y + 52 + i * RowHeight + 10);

        return y + height;
    }

    private void DrawSubscriptionButtons(Ui ui, JsonNode item, double right, double top)
    {
        var id = item.Str("id") ?? "";
        var name = item.Str("tier_name") ?? "this membership";

        if (item.Bool("can_dispute"))
        {
            right -= Widgets.ButtonWidth("Report a problem");

            if (Widgets.Button(ui, "sub-dispute-" + id, right, top, "Report a problem", ButtonKind.Subtle, out _, true, 32))
                OpenDispute(null, id, name);

            right -= 8;
        }

        if (!item.Bool("live") || item.Bool("cancel_at_period_end"))
            return;

        right -= Widgets.ButtonWidth("Unsubscribe");

        if (Widgets.Button(ui, "sub-cancel-" + id, right, top, "Unsubscribe", ButtonKind.Danger, out _, true, 32))
        {
            _cancelId = id;
            _targetName = name;
            _cancelOpen = true;
        }
    }

    private double DrawBought(Ui ui, double left, double y, double width, List<JsonNode> items, Dictionary<string, string> notes)
    {
        var height = 56 + items.Count * RowHeight;
        Card card = Card.Open(ui, "purchases-bought", left, y, width, height, "One-time purchases");

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var top = 52 + i * RowHeight;
            var cents = item.Long("amount_cents");
            var sub = Joined(cents > 0 ? Format.Money(cents, item.Str("currency") ?? "usd") : "", Format.Date(item.Str("purchased_at")), item.Bool("available") ? "" : "No longer listed");
            Row(card, top, width, item.Str("name") ?? "", Joined(sub, Reply(notes, item.Str("payment_id"))), item.Str("dispute_status"));
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (!items[i].Bool("can_dispute"))
                continue;

            var payment = items[i].Str("payment_id") ?? "";
            var buttonWidth = Widgets.ButtonWidth("Report a problem");

            if (Widgets.Button(ui, "buy-dispute-" + payment, left + width - Card.Padding - buttonWidth, y + 52 + i * RowHeight + 10, "Report a problem", ButtonKind.Subtle, out _, true, 32))
                OpenDispute(payment, null, items[i].Str("name") ?? "this purchase");
        }

        return y + height;
    }

    private static string Reply(Dictionary<string, string> notes, string? key)
    {
        var reply = notes.GetValueOrDefault(key ?? "", "");
        return reply.Length > 0 ? "Staff reply: " + reply : "";
    }

    private static void Row(Card card, double top, double width, string name, string detail, string? dispute)
    {
        var right = width - Card.Padding - 260;
        var shown = Chrome.Clip(Fonts.Main, 14.5, name, right - Card.Padding - 120);
        card.Bold(Card.Padding, top + 14, 14.5, shown, Palette.Text);
        card.Text(Card.Padding, top + 36, 13, Chrome.Clip(Fonts.Main, 13, detail, right - Card.Padding), Palette.Muted);
        DisputePill(card, Card.Padding + Fonts.Main.Measure(14.5, shown) + 12, top + 3, dispute);
    }

    private static string Joined(params string[] parts) => string.Join(" · ", parts.Where(part => part.Length > 0));

    private static void DisputePill(Card card, double left, double top, string? status)
    {
        var (text, color) = status switch
        {
            "open" => ("Dispute open", Palette.Warn),
            "accepted" => ("Refunded", Palette.Good),
            "rejected" => ("Dispute closed", Palette.Muted),
            _ => ("", Palette.Muted),
        };

        if (text.Length > 0)
            card.Pill(left, top + 4, text, color);
    }

    private void OpenDispute(string? payment, string? subscription, string name)
    {
        _disputePayment = payment;
        _disputeSubscription = subscription;
        _targetName = name;
        _reason = "";
        _reasonOpen = true;
    }

    private void DrawPurchaseDialogs(Ui ui)
    {
        if (ui.ModalBegin("Report a problem", ref _reasonOpen))
        {
            ui.Wrapped($"Tell MonkeForge staff what went wrong with {_targetName}. If we agree, we'll refund you. Try contacting the creator first if it's something they can fix.");
            ui.Space(6);
            ui.TextEdit("##dispute-reason", ref _reason, 110, EditFlags.Wrap, 2000);
            ui.Space(6);

            if (ui.Button("Send to staff") && _reason.Trim().Length >= 10)
            {
                SendDispute();
                ui.PopupClose();
            }

            ui.SameLine();

            if (ui.Button("Cancel"))
                ui.PopupClose();

            ui.ModalEnd();
        }

        if (!ui.ModalBegin("Unsubscribe?", ref _cancelOpen))
            return;

        ui.Wrapped($"Unsubscribe from {_targetName}? You keep access until the end of the month you already paid for.");
        ui.Space(6);

        if (ui.Button("Unsubscribe") && _cancelId != null)
        {
            Send(HttpMethod.Post, "/api/v1/subscriptions/" + Uri.EscapeDataString(_cancelId) + "/cancel", null, "Unsubscribed.", true);
            ui.PopupClose();
        }

        ui.SameLine();

        if (ui.Button("Keep it"))
            ui.PopupClose();

        ui.ModalEnd();
    }

    private void SendDispute()
    {
        var body = _disputePayment != null
            ? new Dictionary<string, string> { ["payment_id"] = _disputePayment, ["reason"] = _reason }
            : new Dictionary<string, string> { ["subscription_id"] = _disputeSubscription ?? "", ["reason"] = _reason };

        Send(HttpMethod.Post, "/api/v1/disputes", body, "Thanks. Staff will review it and get back to you here.", true);
    }
}
