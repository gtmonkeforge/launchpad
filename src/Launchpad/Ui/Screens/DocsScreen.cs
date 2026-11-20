using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class DocsScreen : IScreen
{
    private const double SidebarWidth = 250;
    private const double SideMargin = PageFrame.SideMargin;
    private const string Home = "index";

    private readonly Shell _shell;
    private readonly string _slug;
    private string _search = "";
    private double _ox;
    private double _oy;
    private MarkdownLayout? _layout;
    private double _layoutWidth;

    public DocsScreen(Shell shell, string? arg)
    {
        _shell = shell;
        _slug = string.IsNullOrWhiteSpace(arg) ? Home : arg.Trim('/');
    }

    public void Draw(Ui ui, double width, double height)
    {
        Load();
        (_ox, _oy) = PageFrame.Begin(ui, "docs", height);
        var mainLeft = SideMargin + SidebarWidth + 30;
        var mainWidth = Math.Max(360, Math.Min(820, width - mainLeft - SideMargin));
        var sidebarBottom = DrawSidebar(ui, 28);
        var mainBottom = DrawMain(ui, mainLeft, mainWidth, 28);
        PageFrame.End(ui, _shell, "docs", Math.Max(sidebarBottom, mainBottom), height, width);
    }

    private double DrawMain(Ui ui, double left, double width, double top)
    {
        if (_error != null && _page == null)
        {
            Card card = Card.Open(ui, "docs-message", left, top, width, 100, "Couldn't load this page");
            card.Hint(0, 46, width, _error, Palette.Bad);
            return top + 100;
        }

        if (_page == null || _index == null)
        {
            ui.Pos(left + width / 2 - 12, top + 80);
            ui.Spinner(24);
            return top + 120;
        }

        var section = _page.Str("section") ?? "";
        var title = _page.Str("title") ?? "";
        var updated = DateTimeOffset.TryParse(_page.Str("updated_at"), out var when) ? when.ToString("MMMM d, yyyy") : "";
        var y = top;

        ui.Pos(left, y);
        ui.Custom("docs-head", width, 92, ItemFlags.NoHit, out Item head);
        Canvas c = ui.Canvas()!;

        if (section.Length > 0)
            Chrome.Text(c, Fonts.Main, 12.5, head.X, head.Y + 9, section.ToUpperInvariant(), Palette.Accent);

        Chrome.BoldText(c, Fonts.Main, 28, head.X, head.Y + 40, Chrome.Clip(Fonts.Main, 28, title, width), Palette.Text);
        Chrome.Text(c, Fonts.Main, 13.5, head.X, head.Y + 72, "Updated " + updated, Palette.Faint);
        y += 104;

        if (_layout == null || _layoutWidth != width)
        {
            _layout = Markdown.Layout(DocsHtml.ToMarkdown(_page.Str("html")), width);
            _layoutWidth = width;
        }

        Markdown.Draw(ui, _layout, "docs-md-", left, y, _ox, _oy, url => PageFrame.OpenLink(_shell, url));
        return DrawPager(ui, left, width, y + _layout.Height + 30);
    }

    private double DrawPager(Ui ui, double left, double width, double y)
    {
        var order = Order();
        var at = order.FindIndex(page => page.Str("slug") == _slug);
        var half = (width - 12) / 2;

        if (at > 0)
            Pager(ui, "docs-prev", left, y, half, "Previous", order[at - 1], false);

        if (at >= 0 && at < order.Count - 1)
            Pager(ui, "docs-next", left + half + 12, y, half, "Next", order[at + 1], true);

        return y + 70;
    }

    private void Pager(Ui ui, string id, double x, double y, double width, string label, JsonNode page, bool right)
    {
        Item box = Chrome.Hit(ui, id, x, y, width, 58);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, 58, Palette.Radius, box.Hovered ? Palette.Surface2 : Palette.Surface, Palette.Line);
        var title = Chrome.Clip(Fonts.Main, 15, page.Str("title") ?? "", width - 36);
        var textX = right ? box.X + width - 18 - Fonts.Main.Measure(15, title) : box.X + 18;
        var labelX = right ? box.X + width - 18 - Fonts.Main.Measure(12, label) : box.X + 18;
        Chrome.Text(c, Fonts.Main, 12, labelX, box.Y + 18, label, Palette.Faint);
        Chrome.BoldText(c, Fonts.Main, 15, textX, box.Y + 40, title, Palette.Text);

        if (box.Clicked)
            Open(page.Str("slug") ?? Home);
    }

    private void Open(string slug) => _shell.Router.Push(new Route(Page.Docs, slug == Home ? null : slug));

    public void Reload()
    {
        _index = null;
        _page = null;
        _error = null;
        _started = false;
    }

    private static JsonNode? _cachedIndex;

    private JsonNode? _index = _cachedIndex;
    private JsonNode? _page;
    private string? _error;
    private bool _started;

    private void Load()
    {
        if (_started)
            return;

        _started = true;

        _ = Task.Run(async () =>
        {
            try
            {
                if (_index == null)
                {
                    _index = await _shell.App.Forge.GetJsonAsync("/api/v1/docs");
                    _cachedIndex = _index;
                }

                _page = await _shell.App.Forge.GetJsonAsync("/api/v1/docs/pages/" + _slug);
            }
            catch (UserFacingException ex)
            {
                _error = ex.Message;
            }

            _shell.Invalidate();
        });
    }

    private List<JsonNode> Pages() => (_index?["pages"] as JsonArray)?.Where(page => page != null).Select(page => page!).ToList() ?? [];

    private List<(string Section, List<JsonNode> Items)> Groups()
    {
        var groups = new List<(string Section, List<JsonNode> Items)>();

        foreach (var page in Pages().Where(page => page.Str("slug") != Home))
        {
            var section = page.Str("section") ?? "";
            var group = groups.FindIndex(existing => existing.Section == section);

            if (group < 0)
                groups.Add((section, [page]));
            else
                groups[group].Items.Add(page);
        }

        return groups.OrderBy(group => group.Section.Length > 0).ToList();
    }

    private List<JsonNode> Order()
    {
        var order = Pages().Where(page => page.Str("slug") == Home).ToList();
        order.AddRange(Groups().SelectMany(group => group.Items));
        return order;
    }
}
