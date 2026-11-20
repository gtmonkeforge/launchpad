using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class DocsScreen
{
    private double DrawSidebar(Ui ui, double top)
    {
        ui.Pos(SideMargin, top);
        ui.Size(SidebarWidth, 38);
        ui.InputEx("##docs-search", ref _search, InputFlags.None, "Search docs", 200);
        var y = top + 54;

        if (_index == null)
            return y;

        if (_search.Trim().Length > 0)
            return DrawResults(ui, y);

        var home = Pages().FirstOrDefault(page => page.Str("slug") == Home);
        y = Row(ui, "docs-nav-home", y, home?.Str("title") is { Length: > 0 } homeTitle ? homeTitle : "Overview", Home);

        foreach (var (section, items) in Groups())
        {
            if (section.Length > 0)
            {
                ui.Pos(SideMargin, y + 8);
                ui.Custom("docs-section-" + section, SidebarWidth, 26, ItemFlags.NoHit, out Item label);
                Chrome.Text(ui.Canvas()!, Fonts.Main, 12, label.X + 12, label.Y + 14, section.ToUpperInvariant(), Palette.Faint);
                y += 36;
            }

            foreach (var page in items)
                y = Row(ui, "docs-nav-" + page.Str("slug"), y, page.Str("title") ?? "", page.Str("slug") ?? Home);
        }

        return y;
    }

    private double Row(Ui ui, string id, double y, string title, string slug)
    {
        Item row = Chrome.Hit(ui, id, SideMargin, y, SidebarWidth, 32);
        Canvas c = ui.Canvas()!;
        var current = slug == _slug;

        if (current)
            c.DrawRRect(row.X, row.Y, SidebarWidth, 32, 8, Palette.Surface2);
        else if (row.Hovered)
            c.DrawRRect(row.X, row.Y, SidebarWidth, 32, 8, Palette.Surface);

        Chrome.Text(c, Fonts.Main, 14, row.X + 12, row.Y + 16, Chrome.Clip(Fonts.Main, 14, title, SidebarWidth - 24), current || row.Hovered ? Palette.Text : Palette.Muted);

        if (row.Clicked && !current)
            Open(slug);

        return y + 34;
    }

    private double DrawResults(Ui ui, double y)
    {
        var words = _search.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
        var matches = new List<(JsonNode Page, int Score, string Snippet)>();

        foreach (var page in Pages())
        {
            var title = (page.Str("title") ?? "").ToLowerInvariant();
            var text = page.Str("text") ?? "";
            var haystack = title + " " + (page.Str("section") ?? "").ToLowerInvariant() + " " + text.ToLowerInvariant();

            if (!words.All(haystack.Contains))
                continue;

            var score = words.Sum(word => (title.Contains(word) ? 10 : 0) + (title.StartsWith(word) ? 5 : 0));
            matches.Add((page, score, Snippet(text, words)));
        }

        if (matches.Count == 0)
        {
            ui.Pos(SideMargin + 12, y);
            ui.Custom("docs-no-match", SidebarWidth - 12, 40, ItemFlags.NoHit, out Item none);
            var lines = Chrome.Wrap(Fonts.Main, 13.5, "No pages match \"" + _search.Trim() + "\".", SidebarWidth - 24, 3);

            for (var i = 0; i < lines.Count; i++)
                Chrome.Text(ui.Canvas()!, Fonts.Main, 13.5, none.X, none.Y + 10 + i * 20, lines[i], Palette.Muted);

            return y + 20 + lines.Count * 20;
        }

        foreach (var (page, _, snippet) in matches.OrderByDescending(match => match.Score))
        {
            var slug = page.Str("slug") ?? Home;
            Item row = Chrome.Hit(ui, "docs-result-" + slug, SideMargin, y, SidebarWidth, 62);
            Canvas c = ui.Canvas()!;

            if (row.Hovered)
                c.DrawRRect(row.X, row.Y, SidebarWidth, 62, 8, Palette.Surface);

            Chrome.BoldText(c, Fonts.Main, 14, row.X + 12, row.Y + 16, Chrome.Clip(Fonts.Main, 14, page.Str("title") ?? "", SidebarWidth - 24), Palette.Text);

            if (snippet.Length > 0)
                Chrome.Text(c, Fonts.Main, 12.5, row.X + 12, row.Y + 40, Chrome.Clip(Fonts.Main, 12.5, snippet, SidebarWidth - 24), Palette.Muted);

            if (row.Clicked)
                Open(slug);

            y += 64;
        }

        return y;
    }

    private static string Snippet(string text, List<string> words)
    {
        var lower = text.ToLowerInvariant();
        var at = words.Select(word => lower.IndexOf(word, StringComparison.Ordinal)).Where(index => index >= 0).DefaultIfEmpty(-1).Min();

        if (at < 0)
            return "";

        var start = Math.Max(0, at - 30);
        var end = Math.Min(text.Length, at + 70);
        return (start > 0 ? "..." : "") + text[start..end].Replace('\n', ' ').Trim();
    }
}
