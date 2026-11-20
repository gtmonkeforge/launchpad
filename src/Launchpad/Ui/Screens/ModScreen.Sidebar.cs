using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private static readonly Dictionary<string, string> CapabilityLabels = new()
    {
        ["auto_update"] = "Auto-updating",
        ["extensions"] = "Extensions",
        ["internet"] = "Networking",
    };

    private bool _whatsNew;

    private double DrawSidebar(Ui ui, double left, double width, double y)
    {
        y = DrawDownloadCard(ui, left, width, y) + 14;
        y = DrawShield(ui, left, width, y) + 14;
        y = DrawDetails(ui, left, width, y) + 14;
        return DrawLinks(ui, left, width, y);
    }

    private double DrawDownloadCard(Ui ui, double left, double width, double y)
    {
        var notes = Notes();
        var wrapped = notes.Select(note => WrapLines(Fonts.Main, 13.5, note.Text, width - 40, 6)).ToList();
        var channels = _mod!["channels"] as JsonArray ?? [];
        var whatsNew = WrapLines(Fonts.Main, 13.5, Current.Str("notes") ?? "", width - 40, 8);
        var height = 74 + 46 + wrapped.Sum(lines => lines.Count * 21 + 8) + (channels.Count > 1 ? 82 : 0) + (whatsNew.Count > 0 ? 34 + (_whatsNew ? whatsNew.Count * 21 + 8 : 0) : 0) + 4;

        Item card = Area(ui, "mod-download", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);

        var latest = "Latest" + (_channel != "stable" ? " · " + _channel : "");
        Chrome.Text(c, Fonts.Main, 12.5, card.X + 20, card.Y + 28, latest, Palette.Muted);
        Chrome.BoldText(c, Fonts.Mono, 19, card.X + 20, card.Y + 56, Current == null ? "No release" : "v" + Current.Str("version"), Palette.Text);

        if (Current != null)
        {
            var date = DateText(Current.Str("released_at"));
            Chrome.Text(c, Fonts.Mono, 12.5, card.X + width - 20 - Fonts.Mono.Measure(12.5, date), card.Y + 28, date, Palette.Faint);
        }

        var (label, enabled, click) = Primary();
        var removing = label == "Remove";
        var text = removing && _confirmRemove ? "Click again to remove" : label;

        if (Button(ui, "mod-card-primary", text, left + 20, y + 74, width - 40, 36, removing ? ButtonStyle.Danger : ButtonStyle.Primary, enabled, removing ? Glyphs.Trash : null))
            click?.Invoke();

        var yy = DrawNotes(ui, notes, wrapped, left + 20, y + 74 + 46);

        if (_working)
        {
            ui.Pos(left + width - 44, y + 40);
            ui.Spinner(20);
        }

        if (channels.Count > 1)
            yy = DrawChannels(ui, channels, left + 20, yy, width - 40);

        if (whatsNew.Count > 0)
            DrawWhatsNew(ui, whatsNew, left + 20, yy);

        return y + height;
    }

    private void DrawWhatsNew(Ui ui, List<string> lines, double x, double y)
    {
        Item toggle = Chrome.Hit(ui, "mod-whats-new", x - 4, y + 4, 140, 26);
        Canvas c = ui.Canvas()!;
        var color = toggle.Hovered ? Palette.Text : Palette.Muted;
        c.DrawTriangle(toggle.X + 6, toggle.Y + 8, toggle.X + 6, toggle.Y + 18, toggle.X + 14, toggle.Y + 13, color);

        if (_whatsNew)
            c.DrawTriangle(toggle.X + 5, toggle.Y + 9, toggle.X + 15, toggle.Y + 9, toggle.X + 10, toggle.Y + 17, color);

        Chrome.Text(c, Fonts.Main, 13.5, toggle.X + 22, toggle.Y + 13, "What's new", color);

        if (toggle.Clicked)
            _whatsNew = !_whatsNew;

        if (!_whatsNew)
            return;

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 13.5, toggle.X + 4, toggle.Y + 42 + i * 21, lines[i], Palette.Muted);
    }

    private double DrawNotes(Ui ui, List<(string Text, uint Color)> notes, List<List<string>> wrapped, double x, double y)
    {
        Item anchor = Area(ui, "mod-notes-anchor", x, y, 1, 1);
        Canvas c = ui.Canvas()!;

        for (var i = 0; i < notes.Count; i++)
        {
            for (var line = 0; line < wrapped[i].Count; line++)
                Chrome.Text(c, Fonts.Main, 13.5, anchor.X, anchor.Y + 10 + line * 21, wrapped[i][line], notes[i].Color);

            var used = wrapped[i].Count * 21 + 8;
            anchor = anchor with { Y = anchor.Y + used };
            y += used;
        }

        return y;
    }

    private double DrawChannels(Ui ui, JsonArray channels, double x, double y, double width)
    {
        Item label = Area(ui, "mod-channel-label", x, y + 6, width, 20);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 13, label.X, label.Y + 10, "Release channel", Palette.Muted);

        var names = channels.Select(channel => channel.Str("name") + (channel.Bool("public") ? "" : " · private") + (channel.Bool("enabled") ? "" : " · disabled")).ToList();
        var selected = Math.Max(0, channels.ToList().FindIndex(channel => channel.Str("name") == _channel));
        ui.Pos(x, y + 32);
        ui.Size(width, 38);

        if (ui.Combo("##mod-channel", ref selected, names))
            SwitchChannel(channels[selected].Str("name") ?? "stable");

        return y + 82;
    }

    private double DrawDetails(Ui ui, double left, double width, double y)
    {
        var rows = new List<(string Label, string Value, bool Mono)> { ("GUID", _mod.Str("guid") ?? "", true) };

        if (_mod.Str("id") != _mod.Str("guid"))
            rows.Add(("Custom ID", _mod.Str("id") ?? "", true));

        rows.Add(("Loaders", string.Join(", ", ReleaseLoaders().Select(LoaderLabel)), false));

        if (_mod["author"].Str("discord_username") is { Length: > 0 } discord)
            rows.Add(("Discord", "@" + discord, true));

        rows.Add(("Published", DateText(_mod.Str("created_at")), false));
        var capabilities = CapabilityLabels.Where(pair => _mod["capabilities"].Bool(pair.Key)).Select(pair => pair.Value).ToList();

        if (capabilities.Count > 0)
            rows.Add(("Capabilities", string.Join("|", capabilities), false));

        var valueWidth = width - 40 - DetailLabel - 8;
        var lines = rows.Select(row => row.Label == "Capabilities" ? new List<string> { row.Value } : Chrome.Wrap(row.Mono ? Fonts.Mono : Fonts.Main, 13, row.Value, valueWidth, 3)).ToList();
        var heights = lines.Select(list => Math.Max(37.0, list.Count * 17 + 20)).ToList();
        var height = 62 + heights.Sum() + 12;
        Item card = Area(ui, "mod-details", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 16, card.X + 20, card.Y + 30, "Details", Palette.Text);
        var top = card.Y + 56;

        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0)
                c.DrawRect(card.X + 20, top, width - 40, 1, Palette.Line);

            Chrome.Text(c, Fonts.Main, 13.5, card.X + 20, top + 19, rows[i].Label, Palette.Muted);
            DrawDetailValue(c, rows[i], lines[i], card.X + 20 + DetailLabel, top);
            top += heights[i];
        }

        DrawGuidCopy(ui, left + 20 + DetailLabel, y + 56, valueWidth, heights[0]);
        return y + height;
    }

    private const double DetailLabel = 96;

    private static void DrawDetailValue(Canvas c, (string Label, string Value, bool Mono) row, List<string> lines, double x, double top)
    {
        if (row.Label == "Capabilities")
        {
            foreach (var name in row.Value.Split('|'))
                x += Widgets.Pill(c, x, top + 7, name, Palette.Accent) + 6;

            return;
        }

        var font = row.Mono ? Fonts.Mono : Fonts.Main;

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, font, 13, x, top + 19 + i * 17, lines[i], Palette.Text);
    }

    private void DrawGuidCopy(Ui ui, double x, double y, double width, double height)
    {
        Item hit = Chrome.Hit(ui, "mod-guid-copy", x, y, width, height);

        if (!hit.Clicked)
            return;

        _shell.Window.Goome.Clipboard = _mod.Str("guid") ?? "";
        _shell.Toast("GUID copied.");
    }

    private double DrawLinks(Ui ui, double left, double width, double y)
    {
        var links = new List<(string Label, string Url)>();

        if (_mod.Str("source_url") is { Length: > 0 } source)
            links.Add(("Source code", source));

        foreach (var link in _mod!["links"] as JsonArray ?? [])
            links.Add((link.Str("label") ?? "", link.Str("url") ?? ""));

        if (links.Count == 0)
            return y;

        var height = 20 + 36 + links.Count * 36 + 8;
        Item card = Area(ui, "mod-links", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 17, card.X + 20, card.Y + 32, "Links", Palette.Text);

        for (var i = 0; i < links.Count; i++)
        {
            Item row = Chrome.Hit(ui, "mod-link-" + i, left + 12, y + 52 + i * 36, width - 24, 34);
            c = ui.Canvas()!;
            Chrome.Text(c, Fonts.Main, 14, row.X + 8, row.Y + 17, Chrome.Clip(Fonts.Main, 14, links[i].Label, width - 60), row.Hovered ? Palette.Accent : Palette.Text);

            if (row.Clicked)
                _shell.Call("shell.open", new JsonObject { ["url"] = links[i].Url });
        }

        return y + height;
    }
}
