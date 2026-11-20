using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public delegate void NavGlyph(Canvas canvas, double x, double y, uint color);

public sealed record NavEntry(string Label, NavGlyph Glyph);

public static class SettingsNav
{
    public const double SideMargin = 30;
    public const double TopMargin = 87;
    public const double Width = 210;
    public const double RowHeight = 36;
    public const double ContentGap = 24;
    public const double RightMargin = 30;

    public const double ContentLeft = SideMargin + Width + ContentGap;

    public static double ContentWidth(double windowWidth) => Math.Max(320, windowWidth - ContentLeft - RightMargin);

    public static int Draw(Ui ui, string id, IReadOnlyList<NavEntry> entries, int selected, double top)
    {
        var clicked = -1;

        for (var i = 0; i < entries.Count; i++)
        {
            Item row = Chrome.Hit(ui, id + i, SideMargin, top + i * RowHeight, Width, RowHeight - 2);
            Canvas c = ui.Canvas()!;

            if (i == selected)
                c.DrawRRect(row.X, row.Y, Width, RowHeight - 2, 8, Palette.Surface2);
            else if (row.Hovered)
                c.DrawRRect(row.X, row.Y, Width, RowHeight - 2, 8, Palette.Surface);

            var color = i == selected || row.Hovered ? Palette.Text : Palette.Muted;
            entries[i].Glyph(c, row.X + 18, row.Y + (RowHeight - 2) / 2, color);
            Chrome.Text(c, Fonts.Main, 14.5, row.X + 38, row.Y + (RowHeight - 2) / 2, entries[i].Label, color);

            if (row.Clicked)
                clicked = i;
        }

        return clicked;
    }

    public static void Header(Ui ui, string id, string title, string? subtitle)
    {
        ui.Pos(SideMargin, 27);
        ui.Custom(id, 600, 36, ItemFlags.NoHit, out Item head);
        Canvas c = ui.Canvas()!;
        Widgets.PageHead(c, head.X, head.Y, title);

        if (subtitle != null)
            Chrome.Text(c, Fonts.Main, 14.5, head.X, head.Y + 51, subtitle, Palette.Muted);
    }
}
