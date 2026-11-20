using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SupportScreen
{
    private string _reply = "";

    private double DrawThread(Ui ui, double left, double width, long id)
    {
        if (!_started)
            Fetch("/api/v1/admin/tickets/" + id);

        Item back = Chrome.Hit(ui, "support-back", left, 28, 150, 24);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 14, back.X, back.Y + 12, "< All tickets", back.Hovered ? Palette.Text : Palette.Muted);

        if (back.Clicked)
            _shell.Router.Push(new Route(Page.Support));

        if (Waiting(ui, left, width, 70, out var bottom))
            return bottom;

        var status = _data.Str("status") ?? "open";
        ui.Pos(left, 62);
        ui.Custom("support-thread-head", width, 70, ItemFlags.NoHit, out Item head);
        Canvas c = ui.Canvas()!;
        var pill = Fonts.Main.Measure(12.5, status) + 18;
        Chrome.BoldText(c, Fonts.Main, 24, head.X, head.Y + 18, Chrome.Clip(Fonts.Main, 24, _data.Str("subject") ?? "", width - pill - 20), Palette.Text);
        Widgets.Pill(c, head.X + width - pill, head.Y + 6, status, StatusColor(status));
        var who = _data!["requester"]?.Str("name") ?? "";
        Chrome.Text(c, Fonts.Main, 13.5, head.X, head.Y + 54, _data.Str("reference") + "  " + who + "  " + _data["requester"]?.Str("email"), Palette.Muted);

        var y = 142.0;
        var x = left;

        foreach (var next in new[] { "open", "pending", "closed" })
        {
            if (next == status)
                continue;

            if (Widgets.Button(ui, "support-status-" + next, x, y, "Mark " + next, ButtonKind.Subtle, out var buttonWidth, true, 34))
                Send(HttpMethod.Patch, "/api/v1/admin/tickets/" + id, new { status = next }, "Ticket marked " + next + ".");

            x += buttonWidth + 8;
        }

        y += 52;

        foreach (var message in (_data["messages"] as JsonArray)?.Where(item => item != null).Select(item => item!) ?? [])
            y = DrawMessage(ui, left, y, width, message);

        return DrawReply(ui, left, y + 6, width, id);
    }

    private double DrawMessage(Ui ui, double left, double y, double width, JsonNode message)
    {
        var direction = message.Str("direction") ?? "in";
        var lines = (message.Str("body") ?? "").Replace("\r", "").Split('\n').SelectMany(line => Chrome.Wrap(Fonts.Main, 14, line, width - 44, 60)).Take(80).ToList();
        var height = 56 + lines.Count * 21;
        ui.Pos(left, y);
        ui.Custom("support-message-" + message.Long("id"), width, height, ItemFlags.NoHit, out Item box);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, height, Palette.RadiusLarge, direction == "note" ? 0xFF211F1A : Palette.Surface, direction == "note" ? 0xFF5E4E2C : Palette.Line);

        var name = message["author"]?.Str("name") ?? message.Str("from") ?? "";
        var label = direction switch { "out" => "Reply", "note" => "Internal note", _ => "Received" };
        var when = DateTimeOffset.TryParse(message.Str("created_at"), out var parsed) ? Format.Ago(parsed) : "";
        var nameWidth = Chrome.BoldText(c, Fonts.Main, 14, box.X + 22, box.Y + 24, name, Palette.Text);
        Chrome.Text(c, Fonts.Main, 13, box.X + 22 + nameWidth + 12, box.Y + 24, label + "  " + when, direction == "out" ? Palette.Accent : Palette.Faint);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14, box.X + 22, box.Y + 54 + i * 21, lines[i], Palette.Muted);

        return y + height + 10;
    }

    private double DrawReply(Ui ui, double left, double y, double width, long id)
    {
        Card.Open(ui, "support-reply", left, y, width, 150, "Write a reply");
        ui.Pos(left + Card.Padding, y + 50);
        ui.Size(width - Card.Padding * 2, 38);
        ui.InputEx("##support-reply", ref _reply, InputFlags.None, "Write a message", 4000);
        var ready = _reply.Trim().Length > 0;

        if (Widgets.Button(ui, "support-send", left + Card.Padding, y + 98, "Send reply", ButtonKind.Primary, out var sendWidth, ready, 36))
            Post(id, "reply");

        if (Widgets.Button(ui, "support-note", left + Card.Padding + sendWidth + 10, y + 98, "Add note", ButtonKind.Subtle, out _, ready, 36))
            Post(id, "note");

        return y + 150;
    }

    private void Post(long id, string kind)
    {
        var body = _reply.Trim();
        _reply = "";
        Send(HttpMethod.Post, "/api/v1/admin/tickets/" + id + "/messages", new { kind, body }, kind == "reply" ? "Reply sent." : "Note added.");
    }

    private string _to = "";
    private string _subject = "";
    private string _message = "";

    private double DrawCompose(Ui ui, double left, double width)
    {
        Item back = Chrome.Hit(ui, "support-compose-back", left, 28, 150, 24);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 14, back.X, back.Y + 12, "< All tickets", back.Hovered ? Palette.Text : Palette.Muted);

        if (back.Clicked)
            _shell.Router.Push(new Route(Page.Support));

        Card.Open(ui, "support-compose", left, 70, width, 330, "New ticket");
        Field(ui, left, 118, width, "##support-to", "Email address", ref _to, 254);
        Field(ui, left, 168, width, "##support-subject", "Subject", ref _subject, 200);
        Field(ui, left, 218, width, "##support-message", "Message", ref _message, 4000);
        var ready = _to.Contains('@') && _subject.Trim().Length > 0 && _message.Trim().Length > 0;

        if (Widgets.Button(ui, "support-compose-send", left + Card.Padding, 276, "Send", ButtonKind.Primary, out _, ready, 36))
            Compose();

        return 400;
    }

    private void Field(Ui ui, double left, double y, double width, string id, string hint, ref string value, int maxBytes)
    {
        ui.Pos(left + Card.Padding, y);
        ui.Size(width - Card.Padding * 2, 38);
        ui.InputEx(id, ref value, InputFlags.None, hint, maxBytes);
    }

    private void Compose()
    {
        var body = new { to = _to.Trim(), subject = _subject.Trim(), body = _message.Trim() };

        Send(HttpMethod.Post, "/api/v1/admin/tickets", body, "Message sent.", result =>
        {
            _to = "";
            _subject = "";
            _message = "";
            _shell.Router.Push(new Route(Page.Support, result["id"]?.ToString()));
        });
    }
}
