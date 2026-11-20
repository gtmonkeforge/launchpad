using System.Text.RegularExpressions;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private static readonly string[] Swatches = ["#dc5c3b", "#f59e0b", "#22c55e", "#14b8a6", "#60cdff", "#6366f1", "#a855f7", "#ec4899", "#e5e7eb"];

    private string _savedAccent = Palette.DefaultAccent;
    private string _accent = Palette.DefaultAccent;
    private string _hex = Palette.DefaultAccent;

    private void InitAppearance()
    {
        var saved = shell.User.Str("accent_color");
        _savedAccent = Regex.IsMatch(saved ?? "", "^#[0-9a-fA-F]{6}$") ? saved!.ToLowerInvariant() : shell.App.Settings.Accent;
        _accent = _savedAccent;
        _hex = _savedAccent;
    }

    private void SetAccent(string hex)
    {
        _accent = hex.ToLowerInvariant();
        _hex = _accent;
        Palette.SetAccent(_accent);
        LaunchpadTheme.Apply(shell.Window.Ui.Theme);
        shell.Invalidate();
    }

    private double DrawAppearance(Ui ui, double left, double width)
    {
        const double height = 330;
        Card card = Card.Open(ui, "appearance-panel", left, TopMargin, width, height, "Accent color");
        card.Hint(0, 44, width, "Used for buttons, links, highlights and the background glow.");
        DrawSwatches(ui, left + Card.Padding, TopMargin + 80);
        DrawHexRow(ui, left + Card.Padding, TopMargin + 128);
        DrawSample(ui, left + Card.Padding, TopMargin + 188);
        DrawSaveBar(ui, left + Card.Padding, TopMargin + 248, width - Card.Padding * 2);
        return TopMargin + height;
    }

    private void DrawSwatches(Ui ui, double x, double y)
    {
        for (var i = 0; i < Swatches.Length; i++)
        {
            Item item = Chrome.Hit(ui, "accent-swatch-" + i, x + i * 40, y, 32, 32);
            Canvas c = ui.Canvas()!;
            var selected = string.Equals(Swatches[i], _accent, StringComparison.OrdinalIgnoreCase);
            var color = 0xFF000000 | Convert.ToUInt32(Swatches[i][1..], 16);

            if (selected)
                c.DrawRRect(item.X - 2, item.Y - 2, 36, 36, 10, Palette.Text);

            c.DrawRRect(item.X, item.Y, 32, 32, 8, color);

            if (selected)
                c.DrawRRectLine(item.X - 1, item.Y - 1, 34, 34, 9, 2, Palette.Surface);

            if (item.Clicked)
                SetAccent(Swatches[i]);
        }
    }

    private void DrawHexRow(Ui ui, double x, double y)
    {
        Item preview = Chrome.Hit(ui, "accent-preview", x, y, 40, 40);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, preview.X, preview.Y, 40, 40, 9, Palette.Bg, Palette.LineStrong);
        c.DrawRRect(preview.X + 5, preview.Y + 5, 30, 30, 5, Palette.Accent);

        ui.Pos(x + 48, y);
        ui.Size(130, 40);

        if (ui.Input("##accent-hex", ref _hex, 8) && Regex.IsMatch(_hex, "^#[0-9a-fA-F]{6}$"))
        {
            _accent = _hex.ToLowerInvariant();
            Palette.SetAccent(_accent);
            LaunchpadTheme.Apply(shell.Window.Ui.Theme);
        }

        if (Widgets.Button(ui, "accent-reset", x + 188, y + 3, "Reset", ButtonKind.Subtle, out _, true, 34))
            SetAccent(Palette.DefaultAccent);
    }

    private void DrawSample(Ui ui, double x, double y)
    {
        Widgets.Button(ui, "accent-sample", x, y, "Primary", ButtonKind.Primary, out var width, true, 36);
        Canvas c = ui.Canvas()!;
        Item area = Chrome.Hit(ui, "accent-sample-chips", x + width + 12, y, 160, 36);
        c = ui.Canvas()!;
        var pill = Widgets.Pill(c, area.X, area.Y + 6, "Selected", Palette.Accent);
        Chrome.Text(c, Fonts.Main, 14, area.X + pill + 12, area.Y + 18, "A link", Palette.Muted);
    }

    private void DrawSaveBar(Ui ui, double x, double y, double width)
    {
        Item bar = Chrome.Hit(ui, "accent-bar", x, y, width, 58);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, bar.X, bar.Y, width, 58, Palette.Radius, Palette.Surface, Palette.Line);
        var note = shell.SignedIn ? "Saved to your account and used on every device." : "Saved on this computer.";
        Chrome.Text(c, Fonts.Main, 13.5, bar.X + 14, bar.Y + 29, note, Palette.Muted);

        var saveWidth = Fonts.Main.Measure(14, "Save") + 30;
        var cancelWidth = Fonts.Main.Measure(14, "Cancel") + 30;

        if (Widgets.Button(ui, "accent-cancel", x + width - 14 - saveWidth - 8 - cancelWidth, y + 12, "Cancel", ButtonKind.Subtle, out _))
            SetAccent(_savedAccent);

        if (Widgets.Button(ui, "accent-save", x + width - 14 - saveWidth, y + 12, "Save", ButtonKind.Primary, out _))
            SaveAccent();
    }

    private void SaveAccent()
    {
        var color = _accent;
        _savedAccent = color;

        _ = Task.Run(async () =>
        {
            try
            {
                shell.App.Settings.Accent = color;
                await shell.App.Settings.SaveAsync();

                if (shell.SignedIn)
                    await shell.App.Forge.SendJsonAsync(HttpMethod.Put, "/api/v1/settings", new { accent_color = color });

                shell.Toast("Accent saved.");
            }
            catch (UserFacingException ex)
            {
                shell.Toast(ex.Message);
            }
        });
    }
}
