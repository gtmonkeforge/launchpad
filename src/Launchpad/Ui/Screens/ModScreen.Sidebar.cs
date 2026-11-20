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

    private double DrawSidebar(Ui ui, double left, double width, double y)
    {
        y = DrawDownloadCard(ui, left, width, y) + 14;
        y = DrawDetails(ui, left, width, y) + 14;
        return DrawLinks(ui, left, width, y);
    }

    private double DrawDownloadCard(Ui ui, double left, double width, double y)
    {
        var notes = Notes();
        var wrapped = notes.Select(note => WrapLines(Fonts.Main, 13.5, note.Text, width - 40, 6)).ToList();
        var channels = _mod!["channels"] as JsonArray ?? [];
        var whatsNew = WrapLines(Fonts.Main, 13.5, Current.Str("notes") ?? "", width - 40, 6);
        var height = 20 + 52 + 6 + wrapped.Sum(lines => lines.Count * 21 + 8) + (channels.Count > 1 ? 82 : 0) + (whatsNew.Count > 0 ? 34 + whatsNew.Count * 21 : 0) + 8;

        Item card = Area(ui, "mod-download", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);

        var latest = "Latest" + (_channel != "stable" ? " · " + _channel : "");
        Chrome.Text(c, Fonts.Main, 12.5, card.X + 20, card.Y + 30, latest, Palette.Faint);
        Chrome.BoldText(c, Fonts.Main, 20, card.X + 20, card.Y + 56, Current == null ? "No release" : "v" + Current.Str("version"), Palette.Text);

        if (Current != null)
        {
            var date = DateText(Current.Str("released_at"));
            Chrome.Text(c, Fonts.Main, 13, card.X + width - 20 - Fonts.Main.Measure(13, date), card.Y + 56, date, Palette.Faint);
        }

        var yy = y + 20 + 52 + 6;
        yy = DrawNotes(ui, notes, wrapped, left + 20, yy);

        if (_working)
        {
            ui.Pos(left + width - 44, y + 20 + 52 - 28);
            ui.Spinner(20);
        }

        if (channels.Count > 1)
            yy = DrawChannels(ui, channels, left + 20, yy, width - 40);

        if (whatsNew.Count > 0)
        {
            Canvas w = ui.Canvas()!;
            Chrome.BoldText(w, Fonts.Main, 13.5, _ox + left + 20, _oy + yy + 12, "What’s new", Palette.Text);

            for (var i = 0; i < whatsNew.Count; i++)
                Chrome.Text(w, Fonts.Main, 13.5, _ox + left + 20, _oy + yy + 38 + i * 21, whatsNew[i], Palette.Muted);
        }

        return y + height;
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
            rows.Add(("Capabilities", string.Join(", ", capabilities), false));

        var height = 20 + 36 + rows.Count * 44 + 4;
        Item card = Area(ui, "mod-details", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 17, card.X + 20, card.Y + 32, "Details", Palette.Text);

        for (var i = 0; i < rows.Count; i++)
        {
            var top = card.Y + 56 + i * 44;
            Chrome.Text(c, Fonts.Main, 12.5, card.X + 20, top + 8, rows[i].Label, Palette.Faint);
            var font = rows[i].Mono ? Fonts.Mono : Fonts.Main;
            Chrome.Text(c, font, 13.5, card.X + 20, top + 28, Chrome.Clip(font, 13.5, rows[i].Value, width - 40), Palette.Text);
        }

        return y + height;
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
