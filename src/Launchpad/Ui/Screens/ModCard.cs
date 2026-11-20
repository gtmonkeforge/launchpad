using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public static class ModCard
{
    private const double Pad = 17;
    private const double ChipHeight = 23;

    private const double AfterAuthor = 119;

    private static double BadgesBottom(ModSummary mod, double width)
    {
        if (mod.Badges.Length == 0)
            return AfterAuthor;

        var rows = ChipRows(mod, width);
        return AfterAuthor + 8 + rows * ChipHeight + (rows - 1) * 5;
    }

    public static double Height(ModSummary mod, double width) => BadgesBottom(mod, width) + 10 + 47 + 14 + 45;

    private static int ChipRows(ModSummary mod, double width)
    {
        var rows = 1;
        var x = 0.0;

        foreach (var badge in mod.Badges)
        {
            var chip = Fonts.Main.Measure(13, badge) + 34;

            if (x > 0 && x + chip > width - 2 * Pad)
            {
                rows++;
                x = 0;
            }

            x += chip + 5;
        }

        return rows;
    }

    public static bool Draw(Ui ui, Shell shell, ModSummary mod, double x, double y, double width, double height)
    {
        var authorWidth = Fonts.Main.Measure(13.5, "by " + mod.AuthorName);
        var authorClicked = AuthorLink(ui, shell, mod, x + Pad, y + 108, authorWidth);
        Item item = Chrome.Hit(ui, "card-" + mod.Id, x, y, width, height);
        Canvas c = ui.Canvas()!;
        var left = item.X;
        var top = item.Y;
        Chrome.Box(c, left, top, width, height, Palette.RadiusLarge, Palette.Surface, item.Hovered ? Palette.LineStrong : Palette.Line);

        DrawTop(c, shell, mod, left, top, width);
        Chrome.BoldText(c, Fonts.Main, 17, left + Pad, top + 85, Chrome.Clip(Fonts.Main, 17, mod.Name, width - 2 * Pad), item.Hovered ? Palette.Accent : Palette.Text);
        var byX = left + Pad;
        var byY = top + 108;
        byX += Chrome.Text(c, Fonts.Main, 13.5, byX, byY, "by " + mod.AuthorName, Palette.Muted) + 8;

        if (mod.AuthorIsAdmin)
        {
            Icons.Shield(c, byX + 6, byY, Palette.Admin);
            byX += 16 + Chrome.BoldText(c, Fonts.Main, 12.5, byX + 16, byY, "Admin", Palette.Admin) + 8;
        }

        if (mod.AuthorIsVerified)
        {
            Icons.Check(c, byX + 6, byY, Palette.Good);
            Chrome.BoldText(c, Fonts.Main, 12.5, byX + 16, byY, "Verified", Palette.Good);
        }

        DrawBadges(c, mod, left, top + AfterAuthor + 8, width);
        var lines = Chrome.Wrap(Fonts.Main, 14, mod.Description, width - 2 * Pad, 2);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14, left + Pad, top + BadgesBottom(mod, width) + 10 + 11 + i * 23.5, lines[i], Palette.Muted);

        DrawFooter(c, mod, left, top, width, height);
        return item.Clicked && !authorClicked;
    }

    private static void DrawTop(Canvas c, Shell shell, ModSummary mod, double left, double top, double width)
    {
        Bitmap? logo = shell.Images.Get(mod.Logo);

        if (logo != null)
            c.DrawBitmap(logo, left + Pad, top + Pad, 46, 46, 11);
        else
        {
            Chrome.Box(c, left + Pad, top + Pad, 46, 46, 11, Palette.Surface2, Palette.Line);
            var letters = Format.Initials(mod.Name);
            Chrome.BoldText(c, Fonts.Main, 17, left + Pad + 23 - Fonts.Main.Measure(17, letters) / 2, top + Pad + 23, letters, Palette.Muted);
        }

        var approved = mod.Status is null or "approved";
        var label = approved ? mod.Category : mod.Status == "pending" ? "Pending review" : mod.Status!;
        var color = approved ? Palette.Muted : mod.Status == "pending" ? Palette.Warn : Palette.Bad;
        var pillWidth = Fonts.Main.Measure(12.5, label) + 18;
        var pillLeft = left + width - Pad - pillWidth;
        c.DrawRRect(pillLeft, top + Pad + 1, pillWidth, 22, 6, Palette.LineStrong);
        c.DrawRRect(pillLeft + 1, top + Pad + 2, pillWidth - 2, 20, 5, Palette.Surface);
        Chrome.Text(c, Fonts.Main, 12.5, pillLeft + 9, top + Pad + 12.5, label, color);
    }

    private static bool AuthorLink(Ui ui, Shell shell, ModSummary mod, double x, double centerY, double width)
    {
        if (mod.AuthorId.Length == 0)
            return false;

        Item link = Chrome.Hit(ui, "card-author-" + mod.Id, x - 2, centerY - 11, width + 4, 22);

        if (link.Hovered)
            ui.Canvas()!.DrawRect(link.X + 2, link.Y + 17, width, 1, Palette.Muted);

        if (!link.Clicked)
            return false;

        shell.Router.Push(new Route(Page.Profile, mod.AuthorId));
        return true;
    }

    private static void DrawBadges(Canvas c, ModSummary mod, double left, double top, double width)
    {
        var x = left + Pad;
        var y = top;

        foreach (var badge in mod.Badges)
        {
            var chip = Fonts.Main.Measure(13, badge) + 34;

            if (x > left + Pad && x + chip > left + width - Pad)
            {
                x = left + Pad;
                y += ChipHeight + 5;
            }

            c.DrawRRect(x, y, chip, ChipHeight, 7, 0x4DE8C886);
            c.DrawRRect(x + 1, y + 1, chip - 2, ChipHeight - 2, 6, Palette.Surface);
            var sx = x + 14;
            var sy = y + ChipHeight / 2;
            c.DrawPoly([sx, sy - 5, sx + 1.4, sy - 1.4, sx + 5, sy, sx + 1.4, sy + 1.4, sx, sy + 5, sx - 1.4, sy + 1.4, sx - 5, sy, sx - 1.4, sy - 1.4], 0xFFE8C886);
            Chrome.Text(c, Fonts.Main, 13, x + 24, y + ChipHeight / 2, badge, 0xFFE8C886);
            x += chip + 5;
        }
    }

    private static void DrawFooter(Canvas c, ModSummary mod, double left, double top, double width, double height)
    {
        c.DrawRect(left + 1, top + height - 45, width - 2, 1, Palette.Line);
        var center = top + height - 23;
        var x = left + Pad;

        Icons.Download(c, x + 6, center, Palette.Faint);
        x += 17 + Chrome.Text(c, Fonts.Mono, 13, x + 17, center, Format.Number(mod.Downloads), Palette.Faint) + 14;
        Icons.Clock(c, x + 6, center, Palette.Faint);
        Chrome.Text(c, Fonts.Mono, 13, x + 17, center, Format.Ago(mod.UpdatedAt), Palette.Faint);

        var right = left + width - Pad;
        var version = "v" + (mod.Version.Length == 0 ? "-" : mod.Version);
        right -= Fonts.Mono.Measure(13, version);
        Chrome.Text(c, Fonts.Mono, 13, right, center, version, Palette.Faint);

        if (mod.Price != null)
        {
            right -= 14 + Fonts.Mono.Measure(13, mod.Price);
            Chrome.BoldText(c, Fonts.Mono, 13, right, center, mod.Price, Palette.Text);
        }
    }
}
