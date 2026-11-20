using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ManageScreen : IScreen
{
    private const double RightGutter = 57;
    private const double MaxFormWidth = 680;

    private static readonly string[] TabTitles = ["Listing", "Release", "Versions", "Channels", "Access", "Pricing"];
    private static readonly string[] Categories = ["Gameplay", "Cosmetic", "Utilities", "Accessibility", "Other"];

    private readonly Shell _shell;
    private readonly string _guid;
    private readonly FilePicker _picker = new();
    private readonly ConfirmDialog _confirm = new();
    private readonly ListingEditor _listing;
    private readonly ReleaseEditor _release;
    private int _tab;
    private bool _busy;
    private string? _error;
    private double _ox;
    private double _oy;

    public ManageScreen(Shell shell, string? arg)
    {
        _shell = shell;
        _guid = arg ?? "";
        _listing = new ListingEditor(shell, _picker);
        _release = new ReleaseEditor(shell, _picker);
    }

    public void Reload() => Load();

    public void Draw(Ui ui, double width, double height)
    {
        if (!_shell.SignedIn)
        {
            GuestCard.Draw(ui, _shell, "manage", width, height, "Manage a mod", "Sign in to edit your mods, ship releases and set prices.", "Sign in", false);
            _shell.Footer.Draw(ui, Palette.TitlebarHeight + height - Footer.Height, width);
            return;
        }

        if (!_started)
        {
            _started = true;
            Load();
        }

        var formWidth = Math.Min(MaxFormWidth, width - FormKit.Left - RightGutter);
        ui.ScrollBegin("manage", height);
        ui.Pos(0, 0);
        ui.Custom("manage-origin", 1, 1, ItemFlags.NoHit, out Item origin);
        _ox = origin.X;
        _oy = origin.Y;
        _listing.OriginX = _ox;
        _listing.OriginY = _oy;

        var y = DrawHead(ui, formWidth);

        if (_loadError != null)
            y = DrawLoadError(ui, formWidth, y);
        else if (_mod == null)
        {
            ui.Pos(FormKit.Left + formWidth / 2 - 12, y + 60);
            ui.Spinner(24);
            y += 120;
        }
        else if (!_allowed)
            y = DrawNotYours(ui, formWidth, y);
        else
            y = _tab switch
            {
                0 => DrawListing(ui, formWidth, y),
                1 => DrawReleaseTab(ui, formWidth, y),
                2 => DrawVersions(ui, formWidth, y),
                3 => DrawChannels(ui, formWidth, y),
                4 => DrawAccess(ui, formWidth, y),
                _ => DrawPricing(ui, formWidth, y),
            };

        ui.Pos(0, y + 40);
        ui.Custom("manage-end", 1, 1, ItemFlags.NoHit, out Item _);
        ui.ScrollEnd();
        _picker.Draw(ui);
        _confirm.Draw(ui);
    }

    private double DrawHead(Ui ui, double width)
    {
        var name = _mod.Str("name") ?? _guid;

        if (Widgets.Button(ui, "manage-back", FormKit.Left, 20, "Back to " + Chrome.Clip(Fonts.Main, 14, name, 240), ButtonKind.Subtle, out _, true, 30))
            _shell.Router.Push(new Route(Page.Mod, _guid));

        Item title = Chrome.Hit(ui, "manage-title", FormKit.Left, 62, width, 36);
        Chrome.BoldText(ui.Canvas()!, Fonts.Main, 28, title.X, title.Y + 18, Chrome.Clip(Fonts.Main, 28, _mod == null ? "Manage a mod" : name, width), Palette.Text);
        Item sub = Chrome.Hit(ui, "manage-sub", FormKit.Left, 100, width, 20);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 14.5, sub.X, sub.Y + 10, "Edit the listing, then ship a new release.", Palette.Muted);

        if (_mod == null || !_allowed)
            return 140;

        var tabWidth = width / TabTitles.Length;

        for (var i = 0; i < TabTitles.Length; i++)
        {
            Item tab = Chrome.Hit(ui, "manage-tab-" + i, FormKit.Left + i * tabWidth, 136, tabWidth - 6, 38);
            Canvas c = ui.Canvas()!;
            var selected = _tab == i;
            Chrome.Box(c, tab.X, tab.Y, tabWidth - 6, 38, Palette.Radius, selected ? Palette.Surface2 : Palette.Surface, selected ? Palette.AccentLine : Palette.Line);
            Chrome.BoldText(c, Fonts.Main, 14, tab.X + (tabWidth - 6) / 2 - Fonts.Main.Measure(14, TabTitles[i]) / 2, tab.Y + 19, TabTitles[i], selected ? Palette.Text : Palette.Muted);

            if (tab.Clicked)
            {
                _tab = i;
                _error = null;
                OpenTab(i);
            }
        }

        return 198;
    }

    private double DrawLoadError(Ui ui, double width, double y)
    {
        y = FormKit.Error(ui, _loadError, y + 10, width);

        if (FormKit.ActionButton(ui, "manage-retry", "Try again", FormKit.Left, y + 6, true))
            Load();

        return y + 50;
    }

    private double DrawNotYours(Ui ui, double width, double y)
    {
        Item box = Chrome.Hit(ui, "manage-not-yours", FormKit.Left, y + 10, width, 70);
        Chrome.BoldText(ui.Canvas()!, Fonts.Main, 17, box.X, box.Y + 14, "Not your mod", Palette.Text);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 14.5, box.X, box.Y + 44, "Only the author or members of its publishing group can manage this mod.", Palette.Muted);
        return y + 100;
    }

    private void Run(string done, Func<Task> work, Action? after = null)
    {
        if (_busy)
            return;

        _busy = true;
        _error = null;
        _shell.Invalidate();

        _ = Task.Run(async () =>
        {
            try
            {
                await work();
                _shell.Toast(done);
                after?.Invoke();
            }
            catch (UserFacingException ex)
            {
                _error = ex.Message;
            }
            catch (Exception ex) when (ex is IOException or HttpRequestException)
            {
                _error = ex.Message;
            }

            _busy = false;
            _shell.Invalidate();
        });
    }

    private string ModUrl(string suffix = "") => "/api/v1/mods/" + Uri.EscapeDataString(_guid) + suffix;
}
