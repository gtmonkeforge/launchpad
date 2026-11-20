using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen : IScreen
{
    private const double TopMargin = SettingsNav.TopMargin;

    private static readonly (string Key, NavEntry Entry)[] Sections =
    [
        ("appearance", new NavEntry("Appearance", Glyphs.Swatches)),
        ("keys", new NavEntry("Access keys", Glyphs.Key)),
        ("channels", new NavEntry("Release channels", Glyphs.Lock)),
        ("purchases", new NavEntry("Purchases", Glyphs.Tray)),
        ("payouts", new NavEntry("Payouts", Glyphs.Wallet)),
        ("devices", new NavEntry("Devices", Glyphs.Server)),
        ("account", new NavEntry("Account", Glyphs.User)),
        ("security", new NavEntry("Sign-in & security", Glyphs.Lock)),
        ("emails", new NavEntry("Email", Glyphs.Bell)),
        ("game", new NavEntry("Game & mod loader", Glyphs.Game)),
    ];

    private static readonly NavEntry[] Entries = Sections.Select(section => section.Entry).ToArray();

    private readonly Shell shell;
    private int _section;
    private int _loadedSection = -1;
    private int _request;
    private string? _error;
    private JsonNode? _data;

    public SettingsScreen(Shell shell, string? arg)
    {
        this.shell = shell;
        _section = Math.Max(0, Array.FindIndex(Sections, section => section.Key == arg));
        InitAppearance();
    }

    public void Reload()
    {
        _loadedSection = -1;
    }

    public void Leave()
    {
        if (_accent != _savedAccent)
            SetAccent(_savedAccent);
    }

    public void Draw(Ui ui, double width, double height)
    {
        ui.ScrollBegin("settings-" + _section, height);
        ui.Pos(0, 0);
        ui.Custom("settings-origin", 1, 1, ItemFlags.NoHit, out _);

        SettingsNav.Header(ui, "settings-title", Sections[_section].Entry.Label, null);
        var clicked = SettingsNav.Draw(ui, "settings-nav-", Entries, _section, TopMargin);

        if (clicked >= 0)
            Open(clicked);

        var bottom = DrawSection(ui, SettingsNav.ContentLeft, SettingsNav.ContentWidth(width));
        shell.Footer.Finish(ui, "settings-end", Math.Max(bottom, TopMargin + Entries.Length * SettingsNav.RowHeight), height, width);
        ui.ScrollEnd();
        DrawPurchaseDialogs(ui);
        DrawSecurityDialogs(ui);
    }

    private void Open(int index)
    {
        if (Sections[index].Key == "game")
        {
            shell.Router.Push(new Route(Page.Game));
            return;
        }

        _section = index;
    }

    private double DrawSection(Ui ui, double left, double width)
    {
        if (_section == 0)
            return DrawAppearance(ui, left, width);

        if (!shell.SignedIn)
            return DrawSignedOut(ui, left, width);

        if (_loadedSection != _section)
            StartLoad();

        return Sections[_section].Key switch
        {
            "keys" => DrawKeys(ui, left, width),
            "channels" => DrawChannels(ui, left, width),
            "purchases" => DrawPurchases(ui, left, width),
            "payouts" => DrawPayouts(ui, left, width),
            "devices" => DrawDevices(ui, left, width),
            "account" => DrawAccount(ui, left, width),
            "security" => DrawSecurity(ui, left, width),
            _ => DrawEmails(ui, left, width),
        };
    }

    private static readonly Dictionary<string, (string Title, string Text)> SignedOut = new()
    {
        ["keys"] = ("Access keys", "Sign in to create keys for publishing from scripts and GitHub Actions."),
        ["channels"] = ("Unlock a release channel", "Sign in to redeem a channel code shared by a mod author."),
        ["purchases"] = ("Purchases", "Sign in to see the mods you've bought."),
        ["payouts"] = ("Get paid", "Connect a Stripe account to sell mods and receive donations. Stripe handles your bank and identity details; MonkeForge never sees them."),
        ["devices"] = ("Injector devices", "No devices yet. Sign in to Launchpad to add this computer."),
        ["account"] = ("Not signed in", "Sign in to publish mods, rate, comment and sync settings."),
        ["security"] = ("Sign-in & security", "Sign in to manage your email, password and passkeys."),
        ["emails"] = ("What we email you", "Sign in to choose which emails MonkeForge sends you."),
    };

    private double DrawSignedOut(Ui ui, double left, double width)
    {
        var (title, text) = SignedOut[Sections[_section].Key];
        var height = 116 + Card.HintHeight(text, width);
        Card card = Card.Open(ui, "settings-signed-out", left, TopMargin, width, height, title);
        card.Hint(0, 46, width, text);

        if (Card.Button(ui, "Sign in", left + Card.Padding, TopMargin + height - 56, 120))
            shell.Router.Push(new Route(Page.SignIn));

        return TopMargin + height;
    }

    private void StartLoad()
    {
        _loadedSection = _section;
        _data = null;
        _error = null;
        var key = Sections[_section].Key;

        if (key == "account")
            _profileFilled = false;

        var request = ++_request;

        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode? result = await Fetch(key);

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

    private async Task<JsonNode?> Fetch(string key)
    {
        var forge = shell.App.Forge;

        switch (key)
        {
            case "emails":
            case "security":
                return await forge.GetJsonAsync("/api/v1/account");
            case "keys":
                return await forge.GetJsonAsync("/api/v1/api-keys");
            case "devices":
                return await forge.GetJsonAsync("/api/v1/injector/devices");
            case "account":
                return await forge.GetJsonAsync("/api/v1/users/" + Uri.EscapeDataString(forge.UserId ?? ""));
            case "payouts":
                return await FetchPayouts();
            case "purchases":
                return await FetchPurchases();
            default:
                return new JsonObject();
        }
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
