using GoomeSharp;

using Launchpad.Infrastructure;

namespace Launchpad.Ui;

using Ui = GoomeSharp.Ui;

public sealed class Titlebar(Shell shell)
{
    private static readonly (string Name, Page Page)[] Tabs =
    [
        ("Browse", Page.Browse),
        ("Library", Page.Library),
        ("Publish", Page.Publish),
        ("Admin", Page.Admin),
    ];

    private const double NotificationWidth = 340;
    private const double GuestMenuWidth = 238;
    private const double SignInWidth = 66;

    private bool _searchFocusWanted;
    private string? _beforeSlash;

    public void FocusSearch() => _searchFocusWanted = true;

    public void Draw(Ui ui, double width)
    {
        var height = Palette.TitlebarHeight;
        ui.Pos(0, 0);
        ui.Custom("titlebar-bg", width, height, ItemFlags.NoHit, out _);
        Canvas c = ui.Canvas()!;
        c.DrawRect(0, 0, width, height, Palette.Titlebar);
        c.DrawRect(0, height - 1, width, 1, Palette.Line);

        var leftEnd = DrawLeft(ui, 6);
        var rightStart = DrawRight(ui, width, leftEnd);
        DrawDragArea(ui, leftEnd, rightStart - leftEnd, height);
    }

    private double DrawLeft(Ui ui, double x)
    {
        var router = shell.Router;
        NavButton(ui, "back", x, router.CanGoBack, Icons.Back, router.Back);
        NavButton(ui, "forward", x + 30, router.CanGoForward, Icons.Forward, router.Forward);
        NavButton(ui, "reload", x + 61, true, Icons.Reload, router.Reload);

        shell.Window.Ui.Canvas()!.DrawBitmap(Brand.Logo, 106, 14, 24, 24, 0, Palette.Accent);
        x = 140;

        foreach (var (name, page) in Tabs)
        {
            if (page == Page.Admin && !shell.IsAdmin)
                continue;

            x += Tab(ui, name, page, x) + 2;
        }

        return x + 8;
    }

    private void NavButton(Ui ui, string id, double x, bool enabled, Action<Canvas, double, double, uint> icon, Action click)
    {
        Item item = Chrome.Hit(ui, id, x, 11, 30, 30);
        Canvas c = ui.Canvas()!;

        if (item.Hovered && enabled)
            c.DrawRRect(item.X, item.Y, 30, 30, 7, Palette.Hover);

        icon(c, item.X + 15, item.Y + 15, enabled ? (item.Hovered ? Palette.Text : Palette.Muted) : Palette.Faint);

        if (item.Clicked && enabled)
            click();
    }

    private double Tab(Ui ui, string name, Page page, double x)
    {
        var font = Fonts.Main;
        var width = font.Measure(14, name) + 20;
        Item item = Chrome.Hit(ui, "tab-" + name, x, 9, width, 34);
        Canvas c = ui.Canvas()!;
        var current = shell.Router.Current.Page == page || (page == Page.Browse && shell.Router.Current.Page == Page.Mod);

        if (current)
            c.DrawRRect(item.X, item.Y, width, 34, 8, Palette.Hover);
        else if (item.Hovered)
            c.DrawRRect(item.X, item.Y, width, 34, 8, Palette.Surface);

        Chrome.Text(c, font, 14, item.X + 10, item.Y + 17, name, current || item.Hovered ? Palette.Text : Palette.Muted);

        if (item.Clicked)
            shell.Router.Push(new Route(page));

        return width;
    }

    private double DrawRight(Ui ui, double width, double leftEnd)
    {
        var x = width - 46;
        WindowButton(ui, "close", x, Icons.Close, true, shell.Window.Quit);
        x -= 46;
        var maximized = shell.Window.Goome.State == WindowState.Maximized;
        WindowButton(ui, "maximize", x, maximized ? Icons.Restore : Icons.Maximize, false, ToggleMaximize);
        x -= 46;
        WindowButton(ui, "minimize", x, Icons.Minimize, false, () => shell.Window.Goome.Minimize());
        x -= 10;

        if (shell.SignedIn)
        {
            x -= 34;
            Profile(ui, x);
            x -= 7 + 34;
            Bell(ui, x);
        }
        else
        {
            x -= SignInWidth;
            SignInButton(ui, x);
            x -= 6 + 34;
            GuestMenuButton(ui, x);
        }

        x -= 10;
        var searchWidth = Math.Clamp(x - leftEnd, 120, 298);
        x -= searchWidth;
        Search(ui, x, searchWidth);
        return x;
    }

    private void ToggleMaximize()
    {
        if (shell.Window.Goome.State == WindowState.Maximized)
            shell.Window.Goome.Restore();
        else
            shell.Window.Goome.Maximize();
    }

    private void WindowButton(Ui ui, string id, double x, Action<Canvas, double, double, uint> icon, bool closing, Action click)
    {
        Item item = Chrome.Hit(ui, id, x, 0, 46, Palette.TitlebarHeight - 1);
        Canvas c = ui.Canvas()!;

        if (item.Hovered)
            c.DrawRect(item.X, item.Y, 46, item.H, closing ? Palette.CloseHover : Palette.Hover);

        icon(c, item.X + 23, item.Y + 26, item.Hovered ? (closing ? 0xFFFFFFFF : Palette.Text) : Palette.Muted);

        if (item.Clicked)
            click();
    }

    private void Bell(Ui ui, double x)
    {
        Item item = Chrome.Hit(ui, "bell", x, 9, 34, 34);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, item.X, item.Y, 34, 34, 9, item.Hovered ? Palette.Hover : Palette.Titlebar, Palette.Line);
        Icons.Bell(c, item.X + 17, item.Y + 16, item.Hovered ? Palette.Text : Palette.Muted);

        shell.Notifications.Watch();
        var unread = shell.Notifications.Items.Count;

        if (unread > 0)
            c.DrawCircle(item.X + 26, item.Y + 8, 4, Palette.Accent);

        if (item.Clicked)
        {
            ui.Custom("notifications-seed", 0, 0, ItemFlags.NoHit, out _);
            ui.Pos(item.X + 34 - NotificationWidth - 20, item.Y + 34);
            ui.Custom("notifications-anchor", NotificationWidth + 20, 1, ItemFlags.NoHit, out _);
            ui.PopupOpen("notifications");
        }

        NotificationMenu(ui);
    }

    private void NotificationMenu(Ui ui)
    {
        if (!ui.PopupBegin("notifications"))
            return;

        var items = shell.Notifications.Items;
        ui.Custom("notifications-width", NotificationWidth, 1, ItemFlags.NoHit, out _);
        ui.Label("Notifications");

        if (items.Count > 0 && ui.Button("Mark all read"))
            shell.Notifications.MarkAllRead();

        ui.Separator();

        if (items.Count == 0)
            ui.Label("You're all caught up.");

        foreach (var item in items.Take(12))
        {
            var body = Chrome.Clip(Fonts.Main, 14, item.Str("body") ?? "", 290);

            if (ui.Selectable(body, false) && Notifications.Link(item) is { Length: > 0 } link)
            {
                ui.PopupClose();
                shell.OpenForge(link);
            }
        }

        ui.PopupEnd();
    }

    private void Profile(Ui ui, double x)
    {
        Item item = Chrome.Hit(ui, "avatar", x, 9, 34, 34);
        Canvas c = ui.Canvas()!;
        var name = shell.User?["name"]?.GetValue<string>() ?? shell.User?["display_name"]?.GetValue<string>() ?? "?";
        Bitmap? picture = shell.Images.Get(shell.User?["avatar"]?.GetValue<string>());

        if (picture != null)
            c.DrawBitmap(picture, item.X, item.Y, 34, 34, 17);
        else
        {
            c.DrawCircle(item.X + 17, item.Y + 17, 17, Palette.Surface2);
            var letters = Format.Initials(name);
            var textWidth = Fonts.Main.Measure(13, letters);
            Chrome.Text(c, Fonts.Main, 13, item.X + 17 - textWidth / 2, item.Y + 17, letters, Palette.Text);
        }

        if (item.Clicked)
            ui.PopupOpen("account");

        AccountMenu(ui, name);
    }

    private void SignInButton(Ui ui, double x)
    {
        Item item = Chrome.Hit(ui, "sign-in", x, 11, SignInWidth, 30);
        Canvas c = ui.Canvas()!;
        c.DrawRRect(item.X, item.Y, SignInWidth, 30, 8, item.Hovered ? 0xFFE46A49 : Palette.Accent);
        Chrome.BoldText(c, Fonts.Main, 13, item.X + SignInWidth / 2 - Fonts.Main.Measure(13, "Sign in") / 2, item.Y + 15, "Sign in", Palette.AccentInk);

        if (item.Clicked)
            shell.Router.Push(new Route(Page.SignIn));
    }

    private void GuestMenuButton(Ui ui, double x)
    {
        Item item = Chrome.Hit(ui, "guest-menu", x, 9, 34, 34);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, item.X, item.Y, 34, 34, 9, item.Hovered ? Palette.Hover : Palette.Titlebar, Palette.Line);
        Icons.Gear(c, item.X + 17, item.Y + 17, item.Hovered ? Palette.Text : Palette.Muted);

        if (item.Clicked)
        {
            ui.Custom("guest-seed", 0, 0, ItemFlags.NoHit, out _);
            ui.Pos(item.X + 34 - GuestMenuWidth - 20, item.Y + 34);
            ui.Custom("guest-anchor", GuestMenuWidth + 20, 1, ItemFlags.NoHit, out _);
            ui.PopupOpen("guest");
        }

        GuestMenu(ui);
    }

    private void GuestMenu(Ui ui)
    {
        if (!ui.PopupBegin("guest"))
            return;

        GuestLabel(ui, "Launchpad");

        if (GuestRow(ui, "Library", Icons.Inbox))
            Go(ui, Page.Library);

        if (GuestRow(ui, "Game & mod loader", (canvas, iconX, iconY, color) => Icons.Category(canvas, "Gameplay", iconX, iconY, color)))
            Go(ui, Page.Game);

        ui.Separator();
        GuestLabel(ui, "Site");

        if (GuestRow(ui, "Get the apps", Icons.Rocket))
        {
            ui.PopupClose();
            shell.OpenForge("/software");
        }

        ui.Separator();

        if (GuestRow(ui, "Sign in", Icons.User))
        {
            ui.PopupClose();
            shell.Router.Push(new Route(Page.SignIn));
        }

        if (GuestRow(ui, "Create an account", Icons.Plus))
        {
            ui.PopupClose();
            shell.OpenForge("/signup");
        }

        ui.PopupEnd();
    }

    private void Go(Ui ui, Page page)
    {
        ui.PopupClose();
        shell.Router.Push(new Route(page));
    }

    private static void GuestLabel(Ui ui, string text)
    {
        ui.Custom("guest-label-" + text, GuestMenuWidth, 24, ItemFlags.NoHit, out Item item);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 12, item.X + 4, item.Y + 14, text, Palette.Faint);
    }

    private static bool GuestRow(Ui ui, string text, Action<Canvas, double, double, uint> icon)
    {
        ui.Custom("guest-row-" + text, GuestMenuWidth, 34, ItemFlags.None, out Item item);
        Canvas c = ui.Canvas()!;

        if (item.Hovered)
            ui.Goome.Cursor = CursorShape.Hand;

        if (item.Hovered)
            c.DrawRRect(item.X, item.Y, GuestMenuWidth, 34, 7, Palette.Hover);

        icon(c, item.X + 16, item.Y + 17, Palette.Muted);
        Chrome.Text(c, Fonts.Main, 13.5, item.X + 36, item.Y + 17, text, Palette.Text);
        return item.Clicked;
    }

    private void AccountMenu(Ui ui, string name)
    {
        if (!ui.PopupBegin("account"))
            return;

        ui.Custom("account-width", 190, 1, ItemFlags.NoHit, out _);
        ui.Label(Chrome.Clip(Fonts.Main, 14, name, 170));
        ui.Separator();

        if (ui.Selectable("Settings", false))
        {
            ui.PopupClose();
            shell.Router.Push(new Route(Page.Settings));
        }

        if (ui.Selectable("Game & mod loader", false))
        {
            ui.PopupClose();
            shell.Router.Push(new Route(Page.Game));
        }

        if (ui.Selectable("Sign out", false))
        {
            ui.PopupClose();
            shell.SignOut();
        }

        ui.PopupEnd();
    }

    private void Search(Ui ui, double x, double width)
    {
        string text = shell.Search;

        if (_searchFocusWanted)
        {
            ui.Pos(x, 9);
            ui.Size(width, 34);
            ui.SetFocus();
            _searchFocusWanted = false;
            _beforeSlash = text;
        }

        var result = SearchBox.Draw(ui, "titlebar-search", ref text, x, 9, width, 34, "Search mods, authors, GUIDs", true);

        if (_beforeSlash != null)
        {
            if (text == _beforeSlash + "/")
                text = _beforeSlash;

            _beforeSlash = null;
        }

        shell.Search = text;

        if (result == 2 && shell.Router.Current.Page != Page.Browse)
            shell.Router.Push(new Route(Page.Browse));
    }

    private void DrawDragArea(Ui ui, double x, double width, double height)
    {
        if (width <= 0)
            return;

        Item item = Chrome.Hit(ui, "titlebar-drag", x, 0, width, height - 1);

        if (item.DoubleClicked)
            ToggleMaximize();
        else if (item.Pressed)
            shell.Window.Goome.Drag();
    }
}
