using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private static readonly string[] TabNames = ["Overview", "Versions", "Community", "Security"];

    private int _tab;
    private MarkdownLayout? _layout;
    private double _layoutWidth;

    private double DrawMain(Ui ui, double left, double width, double y)
    {
        DrawTabBar(ui, left, width, y);
        y += 42 + 14;

        return _tab switch
        {
            0 => DrawOverview(ui, left, width, y),
            1 => DrawVersions(ui, left, width, y),
            2 => DrawOnWebsite(ui, left, width, y, "Community", "Comments, reviews and reports live on monkeforge.org.", "#community"),
            _ => DrawOnWebsite(ui, left, width, y, "Security", "The MonkeShield analysis for this mod is shown on monkeforge.org.", "/monkeshield"),
        };
    }

    private void DrawTabBar(Ui ui, double left, double width, double y)
    {
        var x = 0.0;
        var releases = (_mod!["releases"] as JsonArray)?.Count ?? 0;

        for (var i = 0; i < TabNames.Length; i++)
        {
            var label = TabNames[i];
            var count = i == 1 ? releases.ToString() : "";
            var labelWidth = Fonts.Main.Measure(14.5, label);
            var tabWidth = labelWidth + 32 + (count.Length > 0 ? Fonts.Main.Measure(12, count) + 18 : 0);
            Item tab = Chrome.Hit(ui, "mod-tab-" + i, left + x, y, tabWidth, 42);
            Canvas c = ui.Canvas()!;
            var active = _tab == i;
            var color = active ? Palette.Text : tab.Hovered ? Palette.Text : Palette.Muted;
            Chrome.Text(c, Fonts.Main, 14.5, tab.X + 16, tab.Y + 21, label, color);

            if (count.Length > 0)
            {
                var chipLeft = tab.X + 16 + labelWidth + 8;
                Chrome.Box(c, chipLeft, tab.Y + 11, Fonts.Main.Measure(12, count) + 12, 20, 6, Palette.Surface2, Palette.Line);
                Chrome.Text(c, Fonts.Main, 12, chipLeft + 6, tab.Y + 21, count, Palette.Muted);
            }

            if (active)
                c.DrawRect(tab.X + 8, tab.Y + 40, tabWidth - 16, 2, Palette.Accent);

            if (tab.Clicked)
                _tab = i;

            x += tabWidth;
        }

        Canvas line = ui.Canvas()!;
        line.DrawRect(_ox + left, _oy + y + 41, width, 1, Palette.Line);
    }

    private double DrawOverview(Ui ui, double left, double width, double y)
    {
        var screenshots = (_mod!["screenshots"] as JsonArray)?.Select(item => item?.GetValue<string>() ?? "").ToArray() ?? [];

        if (screenshots.Length > 0)
            y = DrawGallery(ui, left, width, y, screenshots) + 14;

        var notes = _mod["channel"].Str("notes");

        if (!string.IsNullOrWhiteSpace(notes))
            y = DrawNotesPanel(ui, left, width, y, _mod["channel"].Str("name") + " channel notes", notes) + 14;

        return DrawDescription(ui, left, width, y);
    }

    private double DrawNotesPanel(Ui ui, double left, double width, double y, string title, string notes)
    {
        var lines = WrapLines(Fonts.Main, 14, notes, width - 44, 30);
        var height = 22 + 30 + lines.Count * 22 + 18;
        Item box = Area(ui, "mod-notes", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 17, box.X + 22, box.Y + 34, title, Palette.Text);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14, box.X + 22, box.Y + 68 + i * 22, lines[i], Palette.Muted);

        return y + height;
    }

    private double DrawDescription(Ui ui, double left, double width, double y)
    {
        if (_layout == null || _layoutWidth != width)
        {
            _layout = Markdown.Layout(_mod.Str("description"), width - 44);
            _layoutWidth = width;
        }

        var height = Math.Max(60, _layout.Height + 44);
        Item box = Area(ui, "mod-description", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Markdown.Draw(ui, _layout, "mod-md-", left + 22, y + 22, _ox, _oy, url => _shell.Call("shell.open", new JsonObject { ["url"] = url }));
        return y + height;
    }

    private double DrawOnWebsite(Ui ui, double left, double width, double y, string title, string text, string anchor)
    {
        Item box = Area(ui, "mod-web-" + title, left, y, width, 130);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, 130, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 17, box.X + 22, box.Y + 34, title, Palette.Text);
        Chrome.Text(c, Fonts.Main, 14, box.X + 22, box.Y + 64, text, Palette.Muted);

        var path = "/view/" + Uri.EscapeDataString(_guid) + anchor;

        if (Button(ui, "mod-web-open-" + title, "Open on monkeforge.org", left + 22, y + 82, 210, 36, ButtonStyle.Plain))
            _shell.OpenForge(path);

        return y + 130;
    }
}
