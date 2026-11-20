using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private const double HeroPad = 22;
    private const double IconSize = 88;

    private double DrawHero(Ui ui, double width, double y)
    {
        var badges = Strings(_mod!["badges"]);
        var height = Math.Max(badges.Length > 0 ? 98.0 : 92.0, ActionsHeight());
        Item area = Area(ui, "mod-hero", SideMargin, y, width, height);
        Canvas c = ui.Canvas()!;
        DrawIcon(c, area.X, area.Y);

        var textLeft = area.X + IconSize + 20;
        var textWidth = width - IconSize - 20 - ActionsWidth - 20;
        var titleY = badges.Length > 0 ? 11.0 : 28.0;
        Chrome.BoldText(c, Fonts.Main, 28, textLeft, area.Y + titleY, Chrome.Clip(Fonts.Main, 28, _mod.Str("name") ?? "", textWidth), Palette.Text);
        DrawMeta(c, textLeft, area.Y + titleY + 36, textWidth);
        DrawAuthorLink(ui, SideMargin + IconSize + 20, y + titleY + 22);

        if (badges.Length > 0)
            DrawBadgeRow(c, badges, textLeft, area.Y + 68);

        DrawActions(ui, SideMargin + width - ActionsWidth, y);
        return y + height;
    }

    private void DrawAuthorLink(Ui ui, double x, double y)
    {
        var id = _mod!["author"].Str("id");

        if (id == null)
            return;

        var width = 29 + Fonts.Main.Measure(15, _mod["author"].Str("name") ?? "");
        Item link = Chrome.Hit(ui, "mod-author-link", x, y, width, 28);

        if (link.Hovered)
            ui.Canvas()!.DrawRect(link.X + 29, link.Y + 21, width - 29, 1, Palette.Muted);

        if (link.Clicked)
            _shell.Router.Push(new Route(Page.Profile, id));
    }

    private void DrawIcon(Canvas c, double x, double y)
    {
        Bitmap? logo = _shell.Images.Get(_mod.Str("logo"));
        Chrome.Box(c, x, y, IconSize, IconSize, 18, Palette.Surface2, Palette.Line);

        if (logo != null)
        {
            c.DrawBitmap(logo, x, y, IconSize, IconSize, 18);
            return;
        }

        var letters = Format.Initials(_mod.Str("name"));
        Chrome.BoldText(c, Fonts.Main, 28, x + IconSize / 2 - Fonts.Main.Measure(28, letters) / 2, y + IconSize / 2, letters, Palette.Muted);
    }

    private void DrawMeta(Canvas c, double x, double centerY, double maxWidth)
    {
        JsonNode? author = _mod!["author"];
        var right = x + maxWidth;
        var name = author.Str("name") ?? "";
        Bitmap? avatar = _shell.Images.Get(author.Str("avatar"));

        if (avatar != null)
            c.DrawBitmap(avatar, x, centerY - 11, 22, 22, 11);
        else
        {
            c.DrawCircle(x + 11, centerY, 11, Palette.Surface2);
            var letters = Format.Initials(name);
            Chrome.Text(c, Fonts.Main, 10, x + 11 - Fonts.Main.Measure(10, letters) / 2, centerY, letters, Palette.Muted);
        }

        x += 29 + Chrome.Text(c, Fonts.Main, 15, x + 29, centerY, name, Palette.Text) + 10;

        if (author.Bool("is_admin"))
        {
            Icons.Shield(c, x + 6, centerY, Palette.Admin);
            x += 16 + Chrome.BoldText(c, Fonts.Main, 12.5, x + 16, centerY, "Admin", Palette.Admin) + 10;
        }

        if (author.Bool("is_verified"))
        {
            Icons.Check(c, x + 6, centerY, Palette.Good);
            x += 16 + Chrome.BoldText(c, Fonts.Main, 12.5, x + 16, centerY, "Verified", Palette.Good) + 10;
        }

        var pills = new List<(string, uint)> { (_mod.Str("category") ?? "", Palette.Muted) };

        if (_mod["channel"].Str("name") is { } channel && channel != "stable")
            pills.Add((channel, Palette.Accent));

        if (_mod.Str("visibility") is { } visibility && visibility != "public")
            pills.Add((visibility == "private" ? "Private" : "Code-only", Palette.Muted));

        if (_mod.Bool("gated"))
            pills.Add((_mod.Bool("owned") ? "You own this mod" : _mod.Long("price_cents") > 0 ? Format.Money(_mod.Long("price_cents"), _mod.Str("currency") ?? "usd") : "From " + Format.Money(_mod.Long("min_tier_cents"), _mod.Str("currency") ?? "usd") + "/mo", Palette.Accent));

        if (_mod.Bool("uses_injector"))
            pills.Add(("Injector", Palette.Muted));

        foreach (var (text, color) in pills)
        {
            if (x + Fonts.Main.Measure(12.5, text) + 18 > right)
                break;

            x += Pill(c, x, centerY, text, color) + 6;
        }
    }

    private static void DrawBadgeRow(Canvas c, string[] badges, double x, double top)
    {
        foreach (var badge in badges)
        {
            var chip = Fonts.Main.Measure(13, badge) + 34;
            c.DrawRRect(x, top, chip, 23, 7, 0x4DE8C886);
            c.DrawRRect(x + 1, top + 1, chip - 2, 21, 6, Palette.Surface);
            c.DrawPoly([x + 14, top + 6.5, x + 15.4, top + 10, x + 19, top + 11.5, x + 15.4, top + 13, x + 14, top + 16.5, x + 12.6, top + 13, x + 9, top + 11.5, x + 12.6, top + 10], 0xFFE8C886);
            Chrome.Text(c, Fonts.Main, 13, x + 24, top + 11.5, badge, 0xFFE8C886);
            x += chip + 6;
        }
    }
}
