using GoomeSharp;

namespace Launchpad.Ui;

using Ui = GoomeSharp.Ui;

public static class Chrome
{
    public static Item Hit(Ui ui, string id, double x, double y, double w, double h)
    {
        ui.Pos(x, y);
        ui.Custom(id, w, h, ItemFlags.None, out Item item);

        if (item.Hovered)
            ui.Goome.Cursor = CursorShape.Hand;

        return item;
    }

    public static double Text(Canvas c, Font font, double size, double x, double centerY, string text, uint color)
    {
        return c.DrawText(font, size, x, centerY - font.LineHeight(size) / 2, text, color);
    }

    public static double BoldText(Canvas c, Font font, double size, double x, double centerY, string text, uint color)
    {
        c.DrawText(font, size, x + 0.6, centerY - font.LineHeight(size) / 2, text, color);
        return c.DrawText(font, size, x, centerY - font.LineHeight(size) / 2, text, color);
    }

    public static string Clip(Font font, double size, string text, double maxWidth)
    {
        if (font.Measure(size, text) <= maxWidth)
            return text;

        var low = 1;
        var high = text.Length;

        while (low < high)
        {
            var middle = (low + high + 1) / 2;

            if (font.Measure(size, text[..middle] + "...") <= maxWidth)
                low = middle;
            else
                high = middle - 1;
        }

        if (char.IsHighSurrogate(text[low - 1]) && low > 1)
            low--;

        text = text[..low];

        return text + "...";
    }

    public static List<string> Wrap(Font font, double size, string text, double maxWidth, int maxLines)
    {
        var lines = new List<string>();
        var line = "";
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < words.Length; i++)
        {
            var attempt = line.Length == 0 ? words[i] : line + " " + words[i];

            if (font.Measure(size, attempt) <= maxWidth || line.Length == 0)
            {
                line = attempt;
                continue;
            }

            lines.Add(line);
            line = words[i];

            if (lines.Count == maxLines)
                break;
        }

        if (lines.Count < maxLines && line.Length > 0)
            lines.Add(line);

        var used = lines.Sum(l => l.Split(' ').Length);

        if (lines.Count == maxLines && used < words.Length)
            lines[^1] = Clip(font, size, lines[^1] + " " + string.Join(' ', words.Skip(used)), maxWidth);

        return lines.Select(text => Clip(font, size, text, maxWidth)).ToList();
    }

    public static void Box(Canvas c, double x, double y, double w, double h, double radius, uint fill, uint line)
    {
        c.DrawRRect(x, y, w, h, radius, line);
        c.DrawRRect(x + 1, y + 1, w - 2, h - 2, Math.Max(0, radius - 1), fill);
    }
}
