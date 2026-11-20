using System.Globalization;
using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ProfileScreen
{
    private const double Gap = 12;
    private const double RowHeight = 78;

    private double DrawMods(Ui ui, double left, double width, double y)
    {
        if (_mods.Count == 0)
        {
            if (_modsLoading)
            {
                ui.Pos(left + width / 2 - 12, y + 40);
                ui.Spinner(24);
                return y + 100;
            }

            return DrawEmpty(ui, "profile-no-mods", left, y, width, "No mods yet", Own ? "Published mods will appear here." : "This author hasn't published anything yet.", Own ? "Publish a mod" : null);
        }

        var columns = Math.Max(1, (int)((width + Gap) / (260 + Gap)));
        var cardWidth = (width - Gap * (columns - 1)) / columns;
        var myId = shell.User == null ? null : shell.App.Forge.UserId;

        for (var start = 0; start < _mods.Count; start += columns)
        {
            var row = _mods.Skip(start).Take(columns).Select(mod => ModSummary.From(mod, myId)).ToList();
            var rowHeight = row.Max(mod => ModCard.Height(mod, cardWidth));

            for (var i = 0; i < row.Count; i++)
            {
                if (ModCard.Draw(ui, shell, row[i], left + i * (cardWidth + Gap), y, cardWidth, rowHeight))
                    shell.Router.Push(new Route(Page.Mod, row[i].Id));
            }

            y += rowHeight + Gap;
        }

        return DrawPaging(ui, left, width, y + 12);
    }

    private double DrawPaging(Ui ui, double left, double width, double y)
    {
        var pages = Math.Max(1, (int)Math.Ceiling(_modTotal / (double)PageSize));

        if (pages == 1)
            return y;

        var center = left + width / 2;
        var text = $"{_modPage} / {pages}";
        var textWidth = Fonts.Mono.Measure(13, text);

        if (Widgets.Button(ui, "profile-prev", center - textWidth / 2 - 24 - 92, y, "Previous", ButtonKind.Subtle, out _, _modPage > 1))
            _ = Task.Run(() => LoadMods(_modPage - 1));

        ui.Pos(center - textWidth / 2, y);
        ui.Custom("profile-page", textWidth, 34, ItemFlags.NoHit, out Item label);
        Chrome.Text(ui.Canvas()!, Fonts.Mono, 13, label.X, label.Y + 17, text, Palette.Muted);

        if (Widgets.Button(ui, "profile-next", center + textWidth / 2 + 24, y, "Next", ButtonKind.Subtle, out _, _modPage < pages))
            _ = Task.Run(() => LoadMods(_modPage + 1));

        return y + 34;
    }

    private double DrawEmpty(Ui ui, string id, double left, double y, double width, string title, string text, string? button)
    {
        if (button != null && Widgets.EmptyState(ui, id, left, y, width, title, text, button, Icons.Plus))
            shell.Router.Push(new Route(Page.Publish));

        if (button != null)
            return y + 206;

        ui.Pos(left, y);
        ui.Custom(id, width, 150, ItemFlags.NoHit, out Item box);
        Canvas c = ui.Canvas()!;
        Chrome.Dashed(c, box.X, box.Y, width, 150, Palette.RadiusLarge, Palette.LineStrong);
        Chrome.BoldText(c, Fonts.Main, 18, box.X + width / 2 - Fonts.Main.Measure(18, title) / 2, box.Y + 58, title, Palette.Text);
        Chrome.Text(c, Fonts.Main, 14.5, box.X + width / 2 - Fonts.Main.Measure(14.5, text) / 2, box.Y + 92, text, Palette.Muted);
        return y + 150;
    }

    private double DrawMemberships(Ui ui, double left, double width, double y)
    {
        var name = _profile.Str("display_name") ?? "";
        ui.Pos(left, y);
        ui.Custom("profile-tiers-intro", width, 22, ItemFlags.NoHit, out Item intro);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 14.5, intro.X, intro.Y + 11, $"Support {name} with a monthly membership.", Palette.Muted);
        y += 34;

        foreach (JsonNode tier in _tiers)
        {
            var benefits = tier["benefits"] is JsonArray list ? list.Select(item => item?.GetValue<string>() ?? "").ToList() : [];
            var height = 72 + benefits.Count * 22;
            ui.Pos(left, y);
            ui.Custom("profile-tier-" + tier.Str("id"), width, height, ItemFlags.NoHit, out Item box);
            Canvas c = ui.Canvas()!;
            Chrome.Box(c, box.X, box.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
            Chrome.BoldText(c, Fonts.Main, 17, box.X + 22, box.Y + 28, tier.Str("name") ?? "", Palette.Text);
            Chrome.Text(c, Fonts.Mono, 13.5, box.X + 22, box.Y + 54, Format.Money(tier.Long("price_cents"), tier.Str("currency") ?? "usd") + "/mo", Palette.Muted);

            for (var i = 0; i < benefits.Count; i++)
            {
                Icons.Check(c, box.X + 30, box.Y + 76 + i * 22, Palette.Good);
                Chrome.Text(c, Fonts.Main, 14, box.X + 46, box.Y + 76 + i * 22, Chrome.Clip(Fonts.Main, 14, benefits[i], width - 200), Palette.Text);
            }

            var label = tier.Bool("subscribed") ? "Subscribed" : "Subscribe";

            if (tier.Bool("subscribed"))
                Widgets.Pill(c, box.X + width - 22 - Fonts.Main.Measure(12.5, label) - 18, box.Y + 24, label, Palette.Good);
            else if (Widgets.Button(ui, "profile-subscribe-" + tier.Str("id"), left + width - 22 - Widgets.ButtonWidth(label), y + 18, label, ButtonKind.Primary, out _))
                shell.OpenForge(_profile.Str("profile_url") ?? "/");

            y += height + 10;
        }

        return y;
    }

    private double DrawFriends(Ui ui, double left, double width, double y)
    {
        if (_friends.Count == 0 && _requests.Count == 0 && _sent.Count == 0)
            return DrawEmpty(ui, "profile-no-friends", left, y, width, "No friends yet", "Open someone's profile and choose Add friend.", null);

        y = Section(ui, "Requests", _requests, left, width, y, true);
        y = Section(ui, "Sent", _sent, left, width, y, false);
        return Section(ui, "Friends", _friends, left, width, y, false);
    }

    private double Section(Ui ui, string title, List<JsonNode> people, double left, double width, double y, bool incoming)
    {
        if (people.Count == 0)
            return y;

        y = Heading(ui, title, people.Count, left, width, y);

        foreach (JsonNode person in people)
        {
            DrawPerson(ui, person, left, y, width, title, incoming);
            y += 64 + 8;
        }

        return y + 10;
    }

    private void DrawPerson(Ui ui, JsonNode person, double left, double y, double width, string section, bool incoming)
    {
        var id = person.Str("id") ?? "";
        Item row = Chrome.Hit(ui, "profile-person-" + section + id, left, y, width, 64);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, row.X, row.Y, width, 64, Palette.RadiusLarge, Palette.Surface, row.Hovered ? Palette.LineStrong : Palette.Line);
        DrawAvatar(c, shell.Images.Get(person.Str("avatar")), row.X + 16, row.Y + 12, 40, person.Str("name") ?? "?");
        Chrome.BoldText(c, Fonts.Main, 15.5, row.X + 70, row.Y + 24, person.Str("name") ?? "", row.Hovered ? Palette.Accent : Palette.Text);
        var status = person.Str("status") ?? "offline";
        Chrome.Text(c, Fonts.Mono, 13, row.X + 70, row.Y + 45, "@" + person.Str("username") + (section == "Friends" ? " · " + (status == "in_game" ? "In game" : status == "online" ? "Online" : "Offline") : ""), Palette.Muted);

        if (row.Clicked)
            shell.Router.Push(new Route(Page.Profile, id));

        var right = left + width - 16;
        var label = incoming ? "Decline" : section == "Sent" ? "Cancel" : "Remove";
        right -= Widgets.ButtonWidth(label);

        if (Widgets.Button(ui, "profile-person-remove-" + section + id, right, y + 15, label, ButtonKind.Subtle, out _, !_busy.ContainsKey("remove-" + id)))
            RemoveFriend(id, incoming ? "Request declined." : section == "Sent" ? "Request cancelled." : "Friend removed.");

        if (incoming && Widgets.Button(ui, "profile-person-accept-" + id, right - 8 - Widgets.ButtonWidth("Accept"), y + 15, "Accept", ButtonKind.Primary, out _, !_busy.ContainsKey("accept-" + id)))
            Act("accept-" + id, HttpMethod.Put, "/api/v1/friends/" + Uri.EscapeDataString(id), null, "Friend added.");
    }

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
