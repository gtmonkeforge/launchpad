using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SupportScreen
{
    private static readonly string[] Folders = ["open", "pending", "closed", "all", "mine", "unassigned"];
    private static readonly string[] FolderNames = ["Open", "Pending", "Closed", "All", "Mine", "Unassigned"];

    private int _folder;
    private int _loadedFolder = -1;

    private double DrawList(Ui ui, double left, double width)
    {
        PageFrame.Title(ui, "support-title", left, 28, "Support", "Tickets from the support mailbox.");

        if (!_started || _loadedFolder != _folder)
        {
            _loadedFolder = _folder;
            Fetch("/api/v1/admin/tickets?limit=40&folder=" + Folders[_folder]);
        }

        var labels = new string[Folders.Length];

        for (var i = 0; i < labels.Length; i++)
        {
            var count = _data?["counts"]?[Folders[i]]?.GetValue<long>();
            labels[i] = count == null ? FolderNames[i] : FolderNames[i] + " " + count;
        }

        var clicked = PageFrame.Tabs(ui, "support-folder-", left, 108, labels, _folder);

        if (clicked >= 0)
            _folder = clicked;

        if (Widgets.Button(ui, "support-new", left + width - Widgets.ButtonWidth("New ticket"), 106, "New ticket", ButtonKind.Primary, out _, true, 36))
            _shell.Router.Push(new Route(Page.Support, "new"));

        if (Waiting(ui, left, width, 156, out var bottom))
            return bottom;

        var items = (_data?["items"] as JsonArray)?.Where(item => item != null).Select(item => item!).ToList() ?? [];

        if (items.Count == 0)
        {
            Card empty = Card.Open(ui, "support-empty", left, 156, width, 90, "Nothing here");
            empty.Hint(0, 46, width, "No tickets in this folder.");
            return 246;
        }

        var y = 156.0;

        foreach (var ticket in items)
        {
            DrawRow(ui, left, y, width, ticket);
            y += 82;
        }

        return y;
    }

    private void DrawRow(Ui ui, double left, double y, double width, JsonNode ticket)
    {
        var status = ticket.Str("status") ?? "open";
        Item row = Chrome.Hit(ui, "support-ticket-" + ticket.Long("id"), left, y, width, 74);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, row.X, row.Y, width, 74, Palette.RadiusLarge, row.Hovered ? Palette.Surface2 : Palette.Surface, Palette.Line);

        if (ticket.Bool("unread"))
            c.DrawCircle(row.X + 14, row.Y + 24, 4, Palette.Accent);

        var requester = ticket["requester"]?.Str("name") ?? ticket["requester"]?.Str("email") ?? "";
        var pill = Fonts.Main.Measure(12.5, status) + 18;
        Chrome.BoldText(c, Fonts.Main, 15, row.X + 26, row.Y + 24, Chrome.Clip(Fonts.Main, 15, ticket.Str("subject") ?? "", width - pill - 80), Palette.Text);
        Widgets.Pill(c, row.X + width - pill - 18, row.Y + 12, status, StatusColor(status));
        var snippet = (ticket.Str("snippet") ?? "").Replace('\n', ' ');
        var when = DateTimeOffset.TryParse(ticket.Str("updated_at"), out var parsed) ? Format.Ago(parsed) : "";
        Chrome.Text(c, Fonts.Main, 13, row.X + 26, row.Y + 48, Chrome.Clip(Fonts.Main, 13, requester + "  " + snippet, width - 180), Palette.Muted);
        Chrome.Text(c, Fonts.Main, 12.5, row.X + width - 18 - Fonts.Main.Measure(12.5, when), row.Y + 52, when, Palette.Faint);

        if (row.Clicked)
            _shell.Router.Push(new Route(Page.Support, ticket.Long("id").ToString()));
    }
}
