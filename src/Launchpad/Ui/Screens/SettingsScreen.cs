using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen(Shell shell) : IScreen
{
    private const double SideMargin = 30;
    private const double TopMargin = 87;
    private const double NavWidth = 221;
    private const double RightGutter = 57;

    private static readonly string[] Sections = ["Account", "Emails", "Access keys", "Devices", "Security and payments"];

    private int _section;
    private int _loadedSection = -1;
    private int _request;
    private string? _error;
    private JsonNode? _data;

    public void Reload()
    {
        _loadedSection = -1;
    }

    public void Draw(Ui ui, double width, double height)
    {
        ui.ScrollBegin("settings", height);
        ui.Pos(0, 0);
        ui.Custom("settings-origin", 1, 1, ItemFlags.NoHit, out _);

        Item title = Chrome.Hit(ui, "settings-title", SideMargin, TopMargin - 60, 300, 36);
        Chrome.BoldText(ui.Canvas()!, Fonts.Main, 28, title.X, title.Y + 18, "Settings", Palette.Text);

        var left = SideMargin + NavWidth + 24;
        var panelWidth = Math.Min(720, Math.Max(300, width - left - RightGutter));
        DrawNav(ui);
        var bottom = shell.SignedIn ? DrawSection(ui, left, panelWidth) : DrawSignedOut(ui, left, panelWidth);

        ui.Pos(0, Math.Max(bottom, TopMargin + 300) + 40);
        ui.Custom("settings-end", 1, 1, ItemFlags.NoHit, out _);
        ui.ScrollEnd();
    }

    private void DrawNav(Ui ui)
    {
        for (var i = 0; i < Sections.Length; i++)
        {
            Item item = Chrome.Hit(ui, "settings-nav-" + i, SideMargin, TopMargin + i * 40, NavWidth, 34);
            Canvas c = ui.Canvas()!;

            if (_section == i)
                c.DrawRRect(item.X, item.Y, NavWidth, 34, 8, Palette.Surface2);
            else if (item.Hovered)
                c.DrawRRect(item.X, item.Y, NavWidth, 34, 8, Palette.Surface);

            Chrome.Text(c, Fonts.Main, 15, item.X + 14, item.Y + 17, Sections[i], _section == i || item.Hovered ? Palette.Text : Palette.Muted);

            if (item.Clicked)
                _section = i;
        }
    }

    private double DrawSignedOut(Ui ui, double left, double width)
    {
        var text = "Sign in to publish mods, rate, comment and sync settings.";
        var height = 116 + Card.HintHeight(text, width);
        Card card = Card.Open(ui, "settings-signed-out", left, TopMargin, width, height, "Not signed in");
        card.Hint(0, 46, width, text);

        if (Card.Button(ui, "Sign in", left + Card.Padding, TopMargin + height - 56, 120))
            shell.Router.Push(new Route(Page.SignIn));

        return TopMargin + height;
    }

    private double DrawSection(Ui ui, double left, double width)
    {
        if (_loadedSection != _section)
            StartLoad();

        return _section switch
        {
            0 => DrawAccount(ui, left, width),
            1 => DrawEmails(ui, left, width),
            2 => DrawKeys(ui, left, width),
            3 => DrawDevices(ui, left, width),
            _ => DrawSecurity(ui, left, width),
        };
    }

    private static readonly string?[] Paths = ["/api/v1/account", "/api/v1/account", "/api/v1/api-keys", "/api/v1/injector/devices", null];

    private void StartLoad()
    {
        _loadedSection = _section;
        _data = null;
        _error = null;
        var path = Paths[_section];

        if (path == null)
            return;

        var request = ++_request;

        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode result = await shell.App.Forge.GetJsonAsync(path);

                if (request == _request)
                    _data = result;
            }
            catch (UserFacingException ex)
            {
                if (request == _request)
                    _error = ex.Message;
            }

            shell.Invalidate();
        });
    }

    private bool Waiting(Ui ui, double left, double width, out double bottom)
    {
        bottom = TopMargin + 120;

        if (_error != null)
        {
            Card card = Card.Open(ui, "settings-error", left, TopMargin, width, 100, "Couldn't load this page");
            card.Hint(0, 46, width, _error, Palette.Bad);
            return true;
        }

        if (_data == null)
        {
            ui.Pos(left + width / 2 - 12, TopMargin + 60);
            ui.Spinner(24);
            return true;
        }

        return false;
    }

    private void Send(HttpMethod method, string path, object? body, string done, bool reload)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await shell.App.Forge.SendJsonAsync(method, path, body);

                if (done.Length > 0)
                    shell.Toast(done);
            }
            catch (UserFacingException ex)
            {
                shell.Toast(ex.Message);
            }

            if (reload)
                _loadedSection = -1;

            shell.Invalidate();
        });
    }
}
