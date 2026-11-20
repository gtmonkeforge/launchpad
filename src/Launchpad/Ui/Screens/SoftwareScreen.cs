using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;
using Launchpad.App;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SoftwareScreen : IScreen
{
    private const double MostWidth = 900;

    private static readonly string[] Keys = ["", "launchpad", "cli", "openforge"];
    private static readonly string[] Labels = ["All software", "Launchpad", "mforge", "OpenForge"];

    private static readonly (string Key, string Name, string Text, string Platforms)[] Products =
    [
        ("launchpad", "Launchpad", "Desktop app for installing mods and launching Gorilla Tag.", "Windows, Linux"),
        ("cli", "mforge", "Command-line tool for installing and upgrading mods.", "Windows, Linux"),
        ("openforge", "OpenForge", "The server behind this site. Open source, written in C#.", "Self-hosted"),
    ];

    private readonly Shell _shell;
    private readonly int _tab;
    private readonly string _launchpadVersion = typeof(LaunchpadApp).Assembly.GetName().Version?.ToString(3) ?? "2.0.0";
    private string? _forgeVersion;
    private bool _asked;
    private int _count;

    public SoftwareScreen(Shell shell, string? arg)
    {
        _shell = shell;
        _tab = Math.Max(0, Array.IndexOf(Keys, arg ?? ""));
    }

    public void Reload() => _asked = false;

    public void Draw(Ui ui, double width, double height)
    {
        Ask();
        _count = 0;
        PageFrame.Begin(ui, "software", height);
        var column = PageFrame.ColumnWidth(width, MostWidth);
        var left = PageFrame.ColumnLeft(width, column);

        var clicked = PageFrame.Tabs(ui, "software-tab-", left, 28, Labels, _tab);

        if (clicked >= 0 && clicked != _tab)
            _shell.Router.Push(new Route(Page.Software, Keys[clicked].Length == 0 ? null : Keys[clicked]));

        var y = _tab switch
        {
            1 => DrawLaunchpad(ui, left, column, 84),
            2 => DrawCli(ui, left, column, 84),
            3 => DrawOpenForge(ui, left, column, 84),
            _ => DrawHub(ui, left, column, 84),
        };

        PageFrame.End(ui, _shell, "software", y, height, width);
    }

    private double DrawHub(Ui ui, double left, double width, double y)
    {
        y = Header(ui, left, width, y, "Software", "Tools for using MonkeForge outside the browser, and the server that runs it.", []);

        foreach (var product in Products)
        {
            Item row = Chrome.Hit(ui, "software-row-" + product.Key, left, y, width, 74);
            Canvas c = ui.Canvas()!;
            Chrome.Box(c, row.X, row.Y, width, 74, Palette.RadiusLarge, row.Hovered ? Palette.Surface2 : Palette.Surface, Palette.Line);
            Chrome.BoldText(c, Fonts.Main, 16, row.X + 22, row.Y + 26, product.Name, Palette.Text);
            Chrome.Text(c, Fonts.Main, 14, row.X + 22, row.Y + 51, Chrome.Clip(Fonts.Main, 14, product.Text, width - 220), Palette.Muted);
            Chrome.Text(c, Fonts.Main, 13.5, row.X + width - 22 - Fonts.Main.Measure(13.5, product.Platforms), row.Y + 37, product.Platforms, Palette.Faint);

            if (row.Clicked)
                _shell.Router.Push(new Route(Page.Software, product.Key));

            y += 84;
        }

        return y;
    }

    private void Ask()
    {
        if (_asked)
            return;

        _asked = true;

        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode info = await _shell.App.Forge.GetJsonAsync("/api/v2/version");
                _forgeVersion = info["openforge"]?.GetValue<string>();
            }
            catch (Exception)
            {
            }

            _shell.Invalidate();
        });
    }

    private string NextId(string name) => "software-" + name + "-" + _count++;

    private double Header(Ui ui, double left, double width, double y, string title, string text, (string Label, Action Click, ButtonKind Kind)[] buttons, string? chip = null)
    {
        var lines = Chrome.Wrap(Fonts.Main, 15, text, Math.Min(width, 640), 4);
        ui.Pos(left, y);
        ui.Custom(NextId("head"), width, 44 + lines.Count * 24, ItemFlags.NoHit, out Item head);
        Canvas c = ui.Canvas()!;
        var titleWidth = Chrome.BoldText(c, Fonts.Main, 28, head.X, head.Y + 20, title, Palette.Text);

        if (chip != null)
            Widgets.Pill(c, head.X + titleWidth + 14, head.Y + 9, chip, Palette.Muted, true);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 15, head.X, head.Y + 56 + i * 24, lines[i], Palette.Muted);

        y += 44 + lines.Count * 24 + 22;
        var x = left;

        foreach (var button in buttons)
        {
            if (Widgets.Button(ui, NextId("action"), x, y, button.Label, button.Kind, out var buttonWidth, true, 38))
                button.Click();

            x += buttonWidth + 10;
        }

        return buttons.Length == 0 ? y : y + 62;
    }

    private double Heading(Ui ui, double left, double y, string text)
    {
        ui.Pos(left, y);
        ui.Custom(NextId("heading"), 400, 30, ItemFlags.NoHit, out Item box);
        Chrome.BoldText(ui.Canvas()!, Fonts.Main, 20, box.X, box.Y + 15, text, Palette.Text);
        return y + 44;
    }

    private double Paragraph(Ui ui, double left, double width, double y, string text, uint color = Palette.Muted)
    {
        var lines = Chrome.Wrap(Fonts.Main, 14.5, text, width, 10);
        ui.Pos(left, y);
        ui.Custom(NextId("text"), width, lines.Count * 22, ItemFlags.NoHit, out Item box);
        Canvas c = ui.Canvas()!;

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14.5, box.X, box.Y + 11 + i * 22, lines[i], color);

        return y + lines.Count * 22 + 10;
    }

    private double Features(Ui ui, double left, double width, double y, (string Title, string Text)[] items)
    {
        var columnWidth = (width - 14) / 2;
        var rowTop = y;

        for (var i = 0; i < items.Length; i += 2)
        {
            var height = 0.0;

            for (var j = i; j < Math.Min(i + 2, items.Length); j++)
                height = Math.Max(height, 56 + Chrome.Wrap(Fonts.Main, 14, items[j].Text, columnWidth - 36, 8).Count * 21);

            for (var j = i; j < Math.Min(i + 2, items.Length); j++)
            {
                var x = left + (j - i) * (columnWidth + 14);
                ui.Pos(x, rowTop);
                ui.Custom(NextId("feature"), columnWidth, height, ItemFlags.NoHit, out Item box);
                Canvas c = ui.Canvas()!;
                Chrome.Box(c, box.X, box.Y, columnWidth, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
                Chrome.BoldText(c, Fonts.Main, 15.5, box.X + 18, box.Y + 26, items[j].Title, Palette.Text);
                var lines = Chrome.Wrap(Fonts.Main, 14, items[j].Text, columnWidth - 36, 8);

                for (var k = 0; k < lines.Count; k++)
                    Chrome.Text(c, Fonts.Main, 14, box.X + 18, box.Y + 54 + k * 21, lines[k], Palette.Muted);
            }

            rowTop += height + 14;
        }

        return rowTop + 10;
    }

    private double Terminal(Ui ui, double left, double width, double y, string title, string[] lines)
    {
        var height = 40 + lines.Length * 20 + 14;
        ui.Pos(left, y);
        ui.Custom(NextId("terminal"), width, height, ItemFlags.NoHit, out Item box);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, height, Palette.Radius, Palette.Bg, Palette.Line);
        c.DrawRect(box.X, box.Y + 32, width, 1, Palette.Line);
        Chrome.Text(c, Fonts.Mono, 12, box.X + 14, box.Y + 16, title, Palette.Faint);

        for (var i = 0; i < lines.Length; i++)
            Chrome.Text(c, Fonts.Mono, 13, box.X + 14, box.Y + 52 + i * 20, Chrome.Clip(Fonts.Mono, 13, lines[i], width - 28), Palette.Muted);

        return y + height + 14;
    }

    private double CopyField(Ui ui, double left, double width, double y, string command)
    {
        ui.Pos(left, y);
        ui.Custom(NextId("copy"), width - 100, 38, ItemFlags.NoHit, out Item field);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, field.X, field.Y, width - 100, 38, Palette.Radius, Palette.Bg, Palette.LineStrong);
        Chrome.Text(c, Fonts.Mono, 12.5, field.X + 12, field.Y + 19, Chrome.Clip(Fonts.Mono, 12.5, command, width - 126), Palette.Text);

        if (Widgets.Button(ui, NextId("copy-button"), left + width - 92, y, "Copy", ButtonKind.Subtle, out _, true, 38, Glyphs.Copy))
        {
            _shell.Window.Goome.Clipboard = command;
            _shell.Toast("Command copied.");
        }

        return y + 52;
    }
}
