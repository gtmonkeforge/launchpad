using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public enum ButtonStyle
{
    Primary,
    Plain,
    Danger,
}

public sealed partial class ModScreen
{
    private static Item Area(Ui ui, string id, double x, double y, double w, double h)
    {
        ui.Pos(x, y);
        ui.Custom(id, w, h, ItemFlags.NoHit, out Item item);
        return item;
    }

    private bool Button(Ui ui, string id, string text, double x, double y, double w, double h, ButtonStyle style, bool enabled = true)
    {
        Item item = Chrome.Hit(ui, id, x, y, w, h);
        Canvas c = ui.Canvas()!;
        var hovered = item.Hovered && enabled;

        if (style == ButtonStyle.Primary && enabled)
            c.DrawRRect(item.X, item.Y, w, h, Palette.Radius, hovered ? 0xFFE46A49 : Palette.Accent);
        else
            Chrome.Box(c, item.X, item.Y, w, h, Palette.Radius, hovered ? Palette.Hover : Palette.Surface2, style == ButtonStyle.Danger && enabled ? 0x66F08585 : Palette.LineStrong);

        var color = !enabled ? Palette.Faint : style == ButtonStyle.Primary ? Palette.AccentInk : style == ButtonStyle.Danger ? Palette.Bad : Palette.Text;
        var size = h >= 40 ? 15 : 13.5;
        Chrome.BoldText(c, Fonts.Main, size, item.X + w / 2 - Fonts.Main.Measure(size, text) / 2, item.Y + h / 2, text, color);
        return item.Clicked && enabled;
    }

    private double Pill(Canvas c, double x, double centerY, string text, uint color)
    {
        var width = Fonts.Main.Measure(12.5, text) + 18;
        Chrome.Box(c, x, centerY - 11, width, 22, 6, Palette.Surface, Palette.LineStrong);
        Chrome.Text(c, Fonts.Main, 12.5, x + 9, centerY, text, color);
        return width;
    }

    private void Panel(Canvas c, double x, double y, double width, double height) =>
        Chrome.Box(c, _ox + x, _oy + y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);

    private static List<string> WrapLines(Font font, double size, string text, double width, int maxLines)
    {
        var lines = new List<string>();

        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            if (paragraph.Trim().Length == 0)
                continue;

            lines.AddRange(Chrome.Wrap(font, size, paragraph.Trim(), width, maxLines));
        }

        return lines.Take(maxLines).ToList();
    }

    private static string Size(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return (bytes / 1048576.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " MB";

        return Math.Max(1, bytes / 1024) + " KB";
    }

    private static string DateText(string? text) => ParseDate(text).ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly Dictionary<string, string> LoaderLabels = new()
    {
        ["bepinex5"] = "BepInEx 5",
        ["bepinex6"] = "BepInEx 6",
        ["melonloader"] = "MelonLoader",
    };

    private static string LoaderLabel(string? id) => id != null && LoaderLabels.TryGetValue(id, out var label) ? label : id ?? "";
}
