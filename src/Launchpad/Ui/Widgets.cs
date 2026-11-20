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
    public const double IconSpace = 22;

    public static double ButtonWidth(string text, bool icon = false) => Fonts.Main.Measure(14, text) + 30 + (icon ? IconSpace : 0);

    public static bool Button(Ui ui, string id, double x, double y, string text, ButtonKind kind, out double width, bool enabled = true, double height = 34, Action<Canvas, double, double, uint>? icon = null)
    {
        width = ButtonWidth(text, icon != null);
        Item item = Chrome.Hit(ui, id, x, y, width, height);
        Canvas c = ui.Canvas()!;
        var hot = item.Hovered && enabled;

        if (kind == ButtonKind.Primary)
            c.DrawRRect(item.X, item.Y, width, height, Palette.Radius, !enabled ? Palette.AccentLine : hot ? Palette.AccentHot : Palette.Accent);
        else
            Chrome.Box(c, item.X, item.Y, width, height, Palette.Radius, hot ? Palette.Hover : Palette.Surface2, kind == ButtonKind.Danger ? 0x66F08585 : Palette.LineStrong);

        var color = kind == ButtonKind.Primary ? Palette.AccentInk : kind == ButtonKind.Danger ? Palette.Bad : Palette.Text;

        if (!enabled && kind != ButtonKind.Primary)
            color = Palette.Faint;

        var textX = item.X + 15;

        if (icon != null)
        {
            icon(c, item.X + 24, item.Y + height / 2, color);
            textX += IconSpace;
        }

        Chrome.Text(c, Fonts.Main, 14, textX, item.Y + height / 2, text, color);
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

    public static void PageHead(Canvas c, double x, double y, string title)
    {
        Chrome.BoldText(c, Fonts.Main, 28, x, y + 18, title, Palette.Text);
    }

    public static bool EmptyState(Ui ui, string id, double x, double y, double width, string title, string text, string button, Action<Canvas, double, double, uint>? icon = null)
    {
        var height = 206.0;
        Item card = Chrome.Hit(ui, id, x, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Dashed(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.LineStrong);
        var center = card.X + width / 2;
        Chrome.BoldText(c, Fonts.Main, 18, center - Fonts.Main.Measure(18, title) / 2, card.Y + 62, title, Palette.Text);
        var lines = Chrome.Wrap(Fonts.Main, 14.5, text, Math.Min(width - 48, 520), 3);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14.5, center - Fonts.Main.Measure(14.5, lines[i]) / 2, card.Y + 94 + i * 22, lines[i], Palette.Muted);

        var buttonWidth = ButtonWidth(button, icon != null);
        return Button(ui, id + "-button", x + width / 2 - buttonWidth / 2, y + height - 72, button, ButtonKind.Primary, out _, true, 34, icon);
    }

    public static void Notice(Canvas c, double x, double y, double width, string text)
    {
        var lines = Chrome.Wrap(Fonts.Main, 14, text, width - 32, 3);
        var height = 22 + lines.Count * 21;
        Chrome.Box(c, x, y, width, height, Palette.Radius, 0xFF211F1A, 0xFF5E4E2C);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14, x + 16, y + 21 + i * 21, lines[i], Palette.Warn);
    }

    public static double NoticeHeight(double width, string text) => 22 + Chrome.Wrap(Fonts.Main, 14, text, width - 32, 3).Count * 21;
}
