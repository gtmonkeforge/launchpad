using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public static class PageFrame
{
    public const double SideMargin = 30;
    public const double TopMargin = 87;

    public static (double X, double Y) Begin(Ui ui, string id, double height)
    {
        ui.ScrollBegin(id, height);
        ui.Pos(0, 0);
        ui.Custom(id + "-origin", 1, 1, ItemFlags.NoHit, out Item origin);
        return (origin.X, origin.Y);
    }

    public static void End(Ui ui, Shell shell, string id, double bottom, double height, double width)
    {
        shell.Footer.Finish(ui, id + "-end", bottom, height, width);
        ui.ScrollEnd();
    }

    public static double ColumnLeft(double windowWidth, double columnWidth) => Math.Max(SideMargin, (windowWidth - columnWidth) / 2);

    public static double ColumnWidth(double windowWidth, double most) => Math.Min(most, Math.Max(320, windowWidth - SideMargin * 2));

    public static void Title(Ui ui, string id, double x, double y, string title, string? subtitle)
    {
        ui.Pos(x, y);
        ui.Custom(id, 600, subtitle == null ? 36 : 60, ItemFlags.NoHit, out Item head);
        Canvas c = ui.Canvas()!;
        Widgets.PageHead(c, head.X, head.Y, title);

        if (subtitle != null)
            Chrome.Text(c, Fonts.Main, 14.5, head.X, head.Y + 51, subtitle, Palette.Muted);
    }

    public static int Tabs(Ui ui, string id, double x, double y, string[] labels, int selected)
    {
        var clicked = -1;
        var left = x;

        for (var i = 0; i < labels.Length; i++)
        {
            var width = Fonts.Main.Measure(14, labels[i]) + 28;
            Item tab = Chrome.Hit(ui, id + i, left, y, width, 32);
            Canvas c = ui.Canvas()!;

            if (i == selected)
                Chrome.Box(c, tab.X, tab.Y, width, 32, Palette.Radius, Palette.Surface2, Palette.LineStrong);
            else if (tab.Hovered)
                c.DrawRRect(tab.X, tab.Y, width, 32, Palette.Radius, Palette.Surface);

            Chrome.Text(c, Fonts.Main, 14, tab.X + 14, tab.Y + 16, labels[i], i == selected || tab.Hovered ? Palette.Text : Palette.Muted);

            if (tab.Clicked)
                clicked = i;

            left += width + 6;
        }

        return clicked;
    }

    public static void OpenLink(Shell shell, string url)
    {
        if (url.StartsWith("/docs", StringComparison.Ordinal))
            shell.Router.Push(new Route(Page.Docs, url.Length > 6 ? url[6..].Trim('/') : null));
        else if (url.StartsWith('/'))
            shell.OpenForge(url);
        else
            shell.Call("shell.open", new System.Text.Json.Nodes.JsonObject { ["url"] = url });
    }
}
