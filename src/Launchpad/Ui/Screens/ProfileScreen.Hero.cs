using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ProfileScreen
{
    private const double AvatarSize = 96;
    private const double HeroPad = 26;
    private const double ActionWidth = 170;

    private double DrawHero(Ui ui, double left, double width, double y)
    {
        var name = _profile.Str("display_name") ?? "?";
        var textLeft = HeroPad + AvatarSize + 24;
        var textWidth = width - textLeft - HeroPad - ActionWidth;
        var about = AboutLines(textWidth);
        var links = _profile["links"] is JsonArray list ? list.Where(link => link != null).ToList() : [];
        var members = _profile["members"] is JsonArray people ? people.Where(member => member != null).ToList() : [];

        var below = 100.0 + about.Count * 22 + (about.Count > 0 ? 8 : 0);
        var linkRows = links.Count > 0 ? LayoutRows(links.Select(link => Widgets.ButtonWidth(link.Str("label") ?? "", true) * 1.0).ToList(), textWidth, 8) : 0;
        var memberRows = members.Count > 0 ? LayoutRows(members.Select(member => 38 + Fonts.Main.Measure(13.5, member.Str("name") ?? "") + (member.Str("role") == "owner" ? 46 : 0)).ToList(), textWidth, 8) : 0;

        below += linkRows * 38 + memberRows * 36;
        var height = Math.Max(AvatarSize + 2 * HeroPad, below + HeroPad - 10);

        ui.Pos(left, y);
        ui.Custom("profile-hero", width, height, ItemFlags.NoHit, out Item box);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        DrawAvatar(c, shell.Images.Get(_profile.Str("avatar")), box.X + HeroPad, box.Y + HeroPad, AvatarSize, name);

        var x = box.X + textLeft;
        var top = box.Y + HeroPad;
        Chrome.BoldText(c, Fonts.Main, 28, x, top + 20, Chrome.Clip(Fonts.Main, 28, name, textWidth), Palette.Text);
        DrawHandle(c, x, top + 58, textWidth);

        var lineTop = top + 90;

        for (var i = 0; i < about.Count; i++)
            Chrome.Text(c, Fonts.Main, 14.5, x, lineTop + i * 22 + 11, about[i], Palette.Muted);

        lineTop += about.Count * 22 + (about.Count > 0 ? 8 : 0);
        var cursor = DrawLinks(ui, links, left + textLeft, lineTop - box.Y + y, textWidth);
        DrawMembers(ui, members, left + textLeft, cursor, textWidth);
        DrawActions(ui, left + width - HeroPad - ActionWidth, y + HeroPad, ActionWidth);
        return y + height;
    }

    private List<string> AboutLines(double width)
    {
        var lines = new List<string>();

        foreach (var paragraph in (_profile.Str("description") ?? "").Replace("\r", "").Split('\n'))
        {
            if (paragraph.Trim().Length == 0)
            {
                if (lines.Count > 0 && lines[^1].Length > 0)
                    lines.Add("");

                continue;
            }

            lines.AddRange(Chrome.Wrap(Fonts.Main, 14.5, paragraph.Trim(), width, 4));
        }

        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        return lines.Take(8).ToList();
    }

    private static int LayoutRows(List<double> widths, double available, double gap)
    {
        var rows = 1;
        var x = 0.0;

        foreach (var width in widths)
        {
            if (x > 0 && x + width > available)
            {
                rows++;
                x = 0;
            }

            x += width + gap;
        }

        return rows;
    }

    private void DrawHandle(Canvas c, double x, double centerY, double maxWidth)
    {
        x += Chrome.Text(c, Fonts.Mono, 13.5, x, centerY, "@" + _profile.Str("username"), Palette.Muted) + 14;

        if (_profile.Bool("is_admin"))
        {
            Icons.Shield(c, x + 6, centerY, Palette.Admin);
            x += 16 + Chrome.BoldText(c, Fonts.Main, 12.5, x + 16, centerY, "Admin", Palette.Admin) + 12;
        }

        if (_profile.Bool("is_verified"))
        {
            Icons.Check(c, x + 6, centerY, Palette.Good);
            x += 16 + Chrome.BoldText(c, Fonts.Main, 12.5, x + 16, centerY, "Verified", Palette.Good) + 12;
        }

        if (_profile.Bool("is_group"))
            Widgets.Pill(c, x, centerY - Widgets.PillHeight / 2, "Publishing group", Palette.Muted);
    }

    private double DrawLinks(Ui ui, List<JsonNode?> links, double left, double top, double width)
    {
        var x = 0.0;
        var y = top;

        foreach (var link in links)
        {
            var label = link.Str("label") ?? "";
            var buttonWidth = Widgets.ButtonWidth(label, true);

            if (x > 0 && x + buttonWidth > width)
            {
                x = 0;
                y += 38;
            }

            if (Widgets.Button(ui, "profile-link-" + label + link.Str("url"), left + x, y, label, ButtonKind.Subtle, out _, true, 30, Glyphs.Link))
                shell.Call("shell.open", new JsonObject { ["url"] = link.Str("url") });

            x += buttonWidth + 8;
        }

        return links.Count > 0 ? y + 38 : y;
    }

    private void DrawMembers(Ui ui, List<JsonNode?> members, double left, double top, double width)
    {
        var x = 0.0;
        var y = top;

        foreach (var member in members)
        {
            var name = member.Str("name") ?? "";
            var role = member.Str("role") == "owner" ? "owner" : "";
            var chipWidth = 38 + Fonts.Main.Measure(13.5, name) + (role.Length > 0 ? 46 : 0);

            if (x > 0 && x + chipWidth > width)
            {
                x = 0;
                y += 36;
            }

            Item chip = Chrome.Hit(ui, "profile-member-" + member.Str("id"), left + x, y, chipWidth, 30);
            Canvas c = ui.Canvas()!;
            Chrome.Box(c, chip.X, chip.Y, chipWidth, 30, 15, chip.Hovered ? Palette.Hover : Palette.Surface2, Palette.Line);
            DrawAvatar(c, shell.Images.Get(member.Str("avatar")), chip.X + 3, chip.Y + 3, 24, name);
            var end = Chrome.Text(c, Fonts.Main, 13.5, chip.X + 34, chip.Y + 15, name, Palette.Text);

            if (role.Length > 0)
                Chrome.Text(c, Fonts.Mono, 12, chip.X + 34 + end + 8, chip.Y + 15, role, Palette.Faint);

            if (chip.Clicked)
                shell.Router.Push(new Route(Page.Profile, member.Str("id")));

            x += chipWidth + 8;
        }
    }

    private void DrawActions(Ui ui, double left, double top, double width)
    {
        if (Own)
        {
            if (Widgets.Button(ui, "profile-edit", left, top, "Edit profile", ButtonKind.Subtle, out _, true, 36))
                shell.OpenForge(_profile.Str("profile_url") ?? "/settings");

            return;
        }

        if (_profile.Bool("is_group"))
            return;

        if (!shell.SignedIn)
        {
            if (Widgets.Button(ui, "profile-signin", left, top, "Sign in", ButtonKind.Primary, out _, true, 36, Icons.User))
                shell.SignIn();

            if (Widgets.Button(ui, "profile-create", left, top + 44, "Create an account", ButtonKind.Subtle, out _, true, 36))
                shell.OpenForge("/signup");

            return;
        }

        var id = _profile.Str("id") ?? "";

        switch (Relation())
        {
            case "friends":
                if (Widgets.Button(ui, "profile-unfriend", left, top, "Remove friend", ButtonKind.Subtle, out _, !_busy.ContainsKey("remove-" + id), 36))
                    RemoveFriend(id, "Friend removed.");

                break;
            case "incoming":
                if (Widgets.Button(ui, "profile-accept", left, top, "Accept request", ButtonKind.Primary, out _, !_busy.ContainsKey("accept-" + id), 36))
                    AcceptFriend(id);

                break;
            case "sent":
                if (Widgets.Button(ui, "profile-cancel", left, top, "Cancel request", ButtonKind.Subtle, out _, !_busy.ContainsKey("remove-" + id), 36))
                    RemoveFriend(id, "Request cancelled.");

                break;
            default:
                if (Widgets.Button(ui, "profile-add", left, top, "Add friend", ButtonKind.Primary, out _, !_busy.ContainsKey("friend"), 36, Icons.Plus))
                    AddFriend();

                break;
        }

        if (Widgets.Button(ui, "profile-message", left, top + 44, "Message", ButtonKind.Subtle, out _, !_busy.ContainsKey("message"), 36))
            StartChat();
    }

    public static void DrawAvatar(Canvas c, Bitmap? picture, double x, double y, double size, string name)
    {
        if (picture != null)
        {
            c.DrawBitmap(picture, x, y, size, size, size / 2);
            return;
        }

        c.DrawCircle(x + size / 2, y + size / 2, size / 2, Palette.Surface2);
        var letters = Format.Initials(name);
        var font = Math.Max(10, size * 0.32);
        Chrome.Text(c, Fonts.Main, font, x + size / 2 - Fonts.Main.Measure(font, letters) / 2, y + size / 2, letters, Palette.Text);
    }
}
