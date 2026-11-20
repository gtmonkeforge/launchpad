using GoomeSharp;

namespace Launchpad.Ui;

using Ui = GoomeSharp.Ui;

public enum ButtonKind
{
    Primary,
    Subtle,
    Danger,
}

public static class Widgets
{
    public const double PillHeight = 23;

    public static bool Button(Ui ui, string id, double x, double y, string text, ButtonKind kind, out double width, bool enabled = true, double height = 34)
    {
        width = Fonts.Main.Measure(14, text) + 30;
        Item item = Chrome.Hit(ui, id, x, y, width, height);
        Canvas c = ui.Canvas()!;
        var hot = item.Hovered && enabled;

        if (kind == ButtonKind.Primary)
            c.DrawRRect(item.X, item.Y, width, height, Palette.Radius, !enabled ? Palette.AccentLine : hot ? 0xFFE46A49 : Palette.Accent);
        else
            Chrome.Box(c, item.X, item.Y, width, height, Palette.Radius, hot ? Palette.Hover : Palette.Surface2, kind == ButtonKind.Danger ? 0x66F08585 : Palette.LineStrong);

        var color = kind == ButtonKind.Primary ? Palette.AccentInk : kind == ButtonKind.Danger ? Palette.Bad : Palette.Text;

        if (!enabled && kind != ButtonKind.Primary)
            color = Palette.Faint;

        Chrome.Text(c, Fonts.Main, 14, item.X + 15, item.Y + height / 2, text, color);
        return item.Clicked && enabled;
    }

    public static double Pill(Canvas c, double x, double y, string text, uint color, bool mono = false)
    {
        var font = mono ? Fonts.Mono : Fonts.Main;
        var width = font.Measure(12.5, text) + 18;
        Chrome.Box(c, x, y, width, PillHeight, 6, Palette.Surface, color == Palette.Muted ? Palette.LineStrong : (color & 0x00FFFFFF) | 0x66000000);
        Chrome.Text(c, font, 12.5, x + 9, y + PillHeight / 2, text, color);
        return width;
    }

    public static void Tile(Canvas c, Shell shell, double x, double y, double size, string name, string? logo)
    {
        Bitmap? picture = shell.Images.Get(logo);

        if (picture != null)
        {
            c.DrawBitmap(picture, x, y, size, size, 11);
            return;
        }

        Chrome.Box(c, x, y, size, size, 11, Palette.Surface2, Palette.Line);
        var letters = Format.Initials(name);
        Chrome.BoldText(c, Fonts.Main, 17, x + size / 2 - Fonts.Main.Measure(17, letters) / 2, y + size / 2, letters, Palette.Muted);
    }

    public static void PageHead(Canvas c, double left, string title, string? subtitle)
    {
        Chrome.BoldText(c, Fonts.Main, 28, left, 45, title, Palette.Text);

        if (subtitle != null)
            Chrome.Text(c, Fonts.Main, 14.5, left, 78, subtitle, Palette.Muted);
    }

    public static bool EmptyState(Ui ui, string id, double x, double y, double width, string title, string text, string button)
    {
        var height = 170.0;
        Item card = Chrome.Hit(ui, id, x, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        var center = card.X + width / 2;
        Chrome.BoldText(c, Fonts.Main, 18, center - Fonts.Main.Measure(18, title) / 2, card.Y + 40, title, Palette.Text);
        var lines = Chrome.Wrap(Fonts.Main, 14.5, text, Math.Min(width - 48, 520), 3);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14.5, center - Fonts.Main.Measure(14.5, lines[i]) / 2, card.Y + 70 + i * 22, lines[i], Palette.Muted);

        var buttonWidth = Fonts.Main.Measure(14, button) + 30;
        return Button(ui, id + "-button", center - buttonWidth / 2, card.Y + height - 54, button, ButtonKind.Primary, out _);
    }

    public static void Notice(Canvas c, double x, double y, double width, string text)
    {
        var lines = Chrome.Wrap(Fonts.Main, 14, text, width - 32, 3);
        var height = 22 + lines.Count * 21;
        Chrome.Box(c, x, y, width, height, Palette.Radius, 0x14E5B85C, 0x66E5B85C);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14, x + 16, y + 21 + i * 21, lines[i], Palette.Warn);
    }

    public static double NoticeHeight(double width, string text) => 22 + Chrome.Wrap(Fonts.Main, 14, text, width - 32, 3).Count * 21;
}
