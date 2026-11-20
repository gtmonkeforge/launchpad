using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ProfileScreen : IScreen
{
    private const double SideMargin = 30;
    private const double RightGutter = 57;
    private const double TopMargin = 87;

    private readonly Shell shell;
    private readonly string _wanted;
    private int _tab;
    private bool _started;

    public ProfileScreen(Shell shell, string? arg)
    {
        this.shell = shell;
        var parts = (arg ?? "me").Split('#', 2);
        _wanted = parts[0].Length == 0 ? "me" : parts[0];

        if (parts.Length > 1 && parts[1] == "purchases")
            _tab = -1;
    }

    private bool Own => _profile != null && shell.SignedIn && _profile.Str("id") == shell.App.Forge.UserId;

    private string[] TabNames()
    {
        var names = new List<string> { "Mods" };

        if (_tiers.Count > 0)
            names.Add("Memberships");

        if (Own)
        {
            names.Add("Purchases");
            names.Add("Friends");
        }

        return [.. names];
    }

    public void Reload()
    {
        Load();
    }

    public void Draw(Ui ui, double width, double height)
    {
        if (_wanted == "me" && !shell.SignedIn)
        {
            GuestCard.Draw(ui, shell, "profile", width, height, "Your profile", "Sign in to see your profile, your mods and your purchases.", "Sign in", false);
            return;
        }

        if (!_started)
        {
            _started = true;
            Load();
        }

        ui.ScrollBegin("profile", height);
        ui.Pos(0, 0);
        ui.Custom("profile-origin", 1, 1, ItemFlags.NoHit, out _);

        var contentWidth = Math.Max(560, width - SideMargin - RightGutter);
        var bottom = Body(ui, contentWidth, TopMargin - 55);

        var footerTop = Math.Max(bottom + 40, height - Footer.Height);
        shell.Footer.Draw(ui, footerTop, width);
        ui.Pos(0, footerTop + Footer.Height);
        ui.Custom("profile-end", 1, 1, ItemFlags.NoHit, out _);
        ui.ScrollEnd();
    }

    private double Body(Ui ui, double width, double y)
    {
        if (_profile == null)
            return DrawLoading(ui, width, y);

        y = DrawHero(ui, SideMargin, width, y);
        var names = TabNames();

        if (_tab == -1)
            _tab = Math.Max(0, Array.IndexOf(names, "Purchases"));

        if (_tab >= names.Length)
            _tab = 0;

        y = DrawTabBar(ui, SideMargin, width, y + 18, names);
        y += 20;

        return names[_tab] switch
        {
            "Memberships" => DrawMemberships(ui, SideMargin, width, y),
            "Purchases" => DrawPurchases(ui, SideMargin, width, y),
            "Friends" => DrawFriends(ui, SideMargin, width, y),
            _ => DrawMods(ui, SideMargin, width, y),
        };
    }

    private double DrawLoading(Ui ui, double width, double y)
    {
        if (_error == null)
        {
            ui.Pos(SideMargin + width / 2 - 12, y + 80);
            ui.Spinner(24);
            return y + 140;
        }

        Item box = Chrome.Hit(ui, "profile-message", SideMargin, y + 40, 460, 100);
        Canvas c = ui.Canvas()!;
        Chrome.BoldText(c, Fonts.Main, 17, box.X, box.Y + 20, "Couldn't open this profile", Palette.Text);
        Chrome.Text(c, Fonts.Main, 14.5, box.X, box.Y + 50, Chrome.Clip(Fonts.Main, 14.5, _error, 600), Palette.Muted);
        return y + 160;
    }

    private double DrawTabBar(Ui ui, double left, double width, double y, string[] names)
    {
        var x = 0.0;

        for (var i = 0; i < names.Length; i++)
        {
            var label = names[i];
            var count = CountFor(label);
            var labelWidth = Fonts.Main.Measure(14.5, label);
            var tabWidth = labelWidth + 32 + (count.Length > 0 ? Fonts.Main.Measure(12, count) + 18 : 0);
            Item tab = Chrome.Hit(ui, "profile-tab-" + label, left + x, y, tabWidth, 42);
            Canvas c = ui.Canvas()!;
            var active = _tab == i;
            Chrome.Text(c, Fonts.Main, 14.5, tab.X + 16, tab.Y + 21, label, active || tab.Hovered ? Palette.Text : Palette.Muted);

            if (count.Length > 0)
            {
                var chipLeft = tab.X + 16 + labelWidth + 8;
                Chrome.Box(c, chipLeft, tab.Y + 11, Fonts.Main.Measure(12, count) + 12, 20, 6, Palette.Surface2, Palette.Line);
                Chrome.Text(c, Fonts.Main, 12, chipLeft + 6, tab.Y + 21, count, Palette.Muted);
            }

            if (active)
                c.DrawRect(tab.X + 8, tab.Y + 40, tabWidth - 16, 2, Palette.Accent);

            if (tab.Clicked)
                _tab = i;

            x += tabWidth;
        }

        ui.Pos(left, y + 41);
        ui.Custom("profile-tab-rule", width, 1, ItemFlags.NoHit, out Item rule);
        ui.Canvas()!.DrawRect(rule.X, rule.Y, width, 1, Palette.Line);
        return y + 42;
    }

    private string CountFor(string tab) => tab switch
    {
        "Mods" => _profile.Long("mod_count").ToString(),
        "Memberships" => _tiers.Count.ToString(),
        "Friends" => _friends.Count.ToString(),
        _ => "",
    };
}
