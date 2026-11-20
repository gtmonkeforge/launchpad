using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class BrowseScreen(Shell shell) : IScreen
{
    private const double SideMargin = 30;
    private const double TopMargin = 87;
    private const double RightGutter = 57;
    private const double SidebarWidth = 221;
    private const double Gap = 12;

    private static readonly string[] SortNames = ["Recently updated", "Most downloaded", "Name A–Z"];

    private int _sort;
    private string _typed = "";
    private long _typedAt;
    private bool _started;

    public void Reload()
    {
        Load(1);
    }

    public void Draw(Ui ui, double width, double height)
    {
        if (!_started)
        {
            _started = true;
            _typed = shell.Search;
            Load(1);
        }

        WatchSearch();

        ui.ScrollBegin("browse", height);
        ui.Pos(0, 0);
        ui.Custom("browse-origin", 1, 1, ItemFlags.NoHit, out Item origin);

        var mainLeft = SideMargin + SidebarWidth + 24;
        var mainWidth = Math.Max(300, width - mainLeft - RightGutter);

        DrawHead(ui, width);
        DrawSidebar(ui, TopMargin);
        var bottom = DrawMain(ui, mainLeft, mainWidth, TopMargin);

        ui.Pos(0, Math.Max(bottom, SidebarBottom) + 40);
        ui.Custom("browse-end", 1, 1, ItemFlags.NoHit, out _);
        ui.ScrollEnd();
    }

    private void WatchSearch()
    {
        if (shell.Search != _typed)
        {
            _typed = shell.Search;
            _typedAt = Goome.NowMs;
        }

        if (_typedAt != 0 && Goome.NowMs - _typedAt > 250)
        {
            _typedAt = 0;
            Load(1);
        }
        else if (_typedAt != 0)
        {
            shell.Window.Invalidate();
        }
    }

    private void DrawHead(Ui ui, double width)
    {
        Item title = Chrome.Hit(ui, "browse-title", SideMargin, TopMargin - 60, 200, 36);
        Canvas c = ui.Canvas()!;
        Chrome.BoldText(c, Fonts.Main, 28, title.X, title.Y + 18, _mineOnly ? "Your mods" : "Mods", Palette.Text);

        var buttonWidth = 148.0;
        Item publish = Chrome.Hit(ui, "browse-publish", width - RightGutter - buttonWidth, TopMargin - 60, buttonWidth, 36);
        c = ui.Canvas()!;
        c.DrawRRect(publish.X, publish.Y, buttonWidth, 36, Palette.Radius, publish.Hovered ? 0xFFE46A49 : Palette.Accent);
        Icons.Plus(c, publish.X + 20, publish.Y + 18, Palette.AccentInk);
        Chrome.BoldText(c, Fonts.Main, 14.5, publish.X + 36, publish.Y + 18, "Publish a mod", Palette.AccentInk);

        if (publish.Clicked)
            shell.Router.Push(new Route(Page.Publish));
    }

    private double DrawMain(Ui ui, double left, double width, double top)
    {
        DrawToolbar(ui, left, width, top);
        var y = top + 41 + 14;

        if (_error != null)
            return DrawMessage(ui, left, y, "Couldn't load mods", _error);

        if (_items.Count == 0 && _loading)
        {
            ui.Pos(left + width / 2 - 12, y + 60);
            ui.Spinner(24);
            return y + 120;
        }

        if (_items.Count == 0)
            return DrawMessage(ui, left, y, Filtered ? "No mods match" : "No mods yet", Filtered ? "Try a different search or clear a filter." : "No mods have been published yet.");

        y = DrawGrid(ui, left, width, y);
        return DrawPaging(ui, left, width, y + 24);
    }

    private double DrawGrid(Ui ui, double left, double width, double y)
    {
        var columns = Math.Max(1, (int)((width + Gap) / (260 + Gap)));
        var cardWidth = (width - Gap * (columns - 1)) / columns;
        var myId = shell.User == null ? null : shell.App.Forge.UserId;

        for (var start = 0; start < _items.Count; start += columns)
        {
            var row = _items.Skip(start).Take(columns).Select(mod => ModSummary.From(mod, myId)).ToList();
            var rowHeight = row.Max(mod => ModCard.Height(mod, cardWidth));

            for (var i = 0; i < row.Count; i++)
            {
                if (ModCard.Draw(ui, shell, row[i], left + i * (cardWidth + Gap), y, cardWidth, rowHeight))
                    shell.Router.Push(new Route(Page.Mod, row[i].Id));
            }

            y += rowHeight + Gap;
        }

        return y;
    }

    private double DrawMessage(Ui ui, double left, double y, string title, string text)
    {
        Item box = Chrome.Hit(ui, "browse-message", left, y, 400, 120);
        Canvas c = ui.Canvas()!;
        Chrome.BoldText(c, Fonts.Main, 17, box.X, box.Y + 30, title, Palette.Text);
        Chrome.Text(c, Fonts.Main, 14.5, box.X, box.Y + 60, text, Palette.Muted);
        return y + 120;
    }

    private double DrawPaging(Ui ui, double left, double width, double y)
    {
        var pages = Math.Max(1, (int)Math.Ceiling(_total / (double)PageSize));

        if (pages == 1)
            return y;

        var center = left + width / 2;
        PagerButton(ui, "pager-prev", "Previous", center - 150, y, _page > 1, () => Load(_page - 1));
        PagerButton(ui, "pager-next", "Next", center + 62, y, _page < pages, () => Load(_page + 1));

        Item label = Chrome.Hit(ui, "pager-label", center - 50, y, 100, 34);
        var text = $"Page {_page} of {pages}";
        Chrome.Text(ui.Canvas()!, Fonts.Main, 13.5, label.X + 50 - Fonts.Main.Measure(13.5, text) / 2, label.Y + 17, text, Palette.Muted);
        return y + 34;
    }

    private static void PagerButton(Ui ui, string id, string text, double x, double y, bool enabled, Action click)
    {
        Item item = Chrome.Hit(ui, id, x, y, 88, 34);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, item.X, item.Y, 88, 34, 8, item.Hovered && enabled ? Palette.Hover : Palette.Surface2, Palette.LineStrong);
        Chrome.Text(c, Fonts.Main, 13.5, item.X + 44 - Fonts.Main.Measure(13.5, text) / 2, item.Y + 17, text, enabled ? Palette.Text : Palette.Faint);

        if (item.Clicked && enabled)
            click();
    }
}
