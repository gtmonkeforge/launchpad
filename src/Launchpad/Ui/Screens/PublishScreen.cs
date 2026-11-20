using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class PublishScreen(Shell shell) : IScreen
{
    private const double SideMargin = 30;
    private const double RightGutter = 57;
    private const double MaxFormWidth = 680;

    private static readonly string[] Categories = ["Gameplay", "Cosmetic", "Utilities", "Accessibility", "Other"];
    private static readonly string[] Visibilities = ["Public · reviewed", "Private · only you", "Requires a channel code"];
    private static readonly string[] VisibilityValues = ["public", "private", "code"];
    private static readonly string[] StepTitles = ["Identity", "Page", "Release"];
    private static readonly string[] StepHints = ["Name, GUID, visibility", "Description, images, links", "Version and downloads"];
    private static readonly string[] LoaderIds = ["bepinex5", "bepinex6", "melonloader"];
    private static readonly string[] LoaderNames = ["BepInEx 5", "BepInEx 6", "MelonLoader"];
    private static readonly string[] CapabilityIds = ["auto_update", "extensions", "internet"];
    private static readonly string[] CapabilityNames = ["Auto-updating", "Extensions", "Networking"];

    private int _step;
    private string _name = "";
    private string _guid = "";
    private string _slug = "";
    private int _category;
    private int _visibility;
    private int _author;
    private string _source = "";
    private bool _injector;
    private string _description = "";
    private string _logoPath = "";
    private string _bannerPath = "";
    private readonly string[] _linkLabels = ["", "", ""];
    private readonly string[] _linkUrls = ["", "", ""];
    private readonly bool[] _capabilities = [false, false, false];
    private string _version = "";
    private string _notes = "";
    private readonly string[] _dllPaths = ["", "", ""];
    private readonly string[] _zipPaths = ["", "", ""];

    private List<Author>? _authors;
    private Task? _loadingAuthors;
    private bool _busy;
    private string? _error;
    private string? _publishedGuid;

    private sealed record Author(string Id, string Name, bool Verified);

    public void Reload()
    {
        _authors = null;
        _loadingAuthors = null;
    }

    public void Draw(Ui ui, double width, double height)
    {
        if (!shell.SignedIn)
        {
            DrawSignIn(ui, width);
            return;
        }

        _loadingAuthors ??= LoadAuthors();
        var formWidth = Math.Min(MaxFormWidth, width - SideMargin - RightGutter);

        ui.ScrollBegin("publish", height);
        ui.Pos(0, 0);
        ui.Custom("publish-origin", 1, 1, ItemFlags.NoHit, out Item _);

        var y = DrawHead(ui, formWidth);

        if (_publishedGuid != null)
            y = DrawDone(ui, y);
        else if (_authors == null)
        {
            ui.Pos(SideMargin + formWidth / 2 - 12, y + 60);
            ui.Spinner(24);
            y += 120;
        }
        else
            y = DrawStep(ui, formWidth, y);

        ui.Pos(0, y + 40);
        ui.Custom("publish-end", 1, 1, ItemFlags.NoHit, out Item _);
        ui.ScrollEnd();
    }

    private double DrawHead(Ui ui, double formWidth)
    {
        Item title = Chrome.Hit(ui, "publish-title", SideMargin, 27, formWidth, 36);
        Canvas c = ui.Canvas()!;
        Chrome.BoldText(c, Fonts.Main, 28, title.X, title.Y + 18, "Publish a mod", Palette.Text);

        Item sub = Chrome.Hit(ui, "publish-sub", SideMargin, 66, formWidth, 20);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 14.5, sub.X, sub.Y + 10, "Three steps: identity, page, release.", Palette.Muted);

        if (_publishedGuid != null)
            return 110;

        var tabWidth = (formWidth - 16) / 3;

        for (var i = 0; i < 3; i++)
        {
            Item tab = Chrome.Hit(ui, "publish-tab-" + i, SideMargin + i * (tabWidth + 8), 102, tabWidth, 54);
            c = ui.Canvas()!;
            var selected = _step == i;
            Chrome.Box(c, tab.X, tab.Y, tabWidth, 54, Palette.Radius, selected ? Palette.Surface2 : Palette.Surface, selected ? Palette.AccentLine : Palette.Line);
            Chrome.BoldText(c, Fonts.Mono, 13, tab.X + 14, tab.Y + 27, "0" + (i + 1), selected ? Palette.Accent : Palette.Faint);
            Chrome.BoldText(c, Fonts.Main, 14.5, tab.X + 48, tab.Y + 18, StepTitles[i], Palette.Text);
            Chrome.Text(c, Fonts.Main, 12.5, tab.X + 48, tab.Y + 37, Chrome.Clip(Fonts.Main, 12.5, StepHints[i], tabWidth - 58), Palette.Muted);

            if (tab.Clicked)
                _step = i;
        }

        return 180;
    }

    private double DrawDone(Ui ui, double y)
    {
        Item box = Chrome.Hit(ui, "publish-done", SideMargin, y, 520, 90);
        Canvas c = ui.Canvas()!;
        Chrome.BoldText(c, Fonts.Main, 17, box.X, box.Y + 16, "Published", Palette.Good);
        Chrome.Text(c, Fonts.Main, 14.5, box.X, box.Y + 46, "Your mod was sent to MonkeForge. Public mods are reviewed first.", Palette.Muted);

        if (ActionButton(ui, "publish-view", "View the mod", SideMargin, y + 80, 150, true))
            shell.Router.Push(new Route(Page.Mod, _publishedGuid));

        return y + 140;
    }

    private void DrawSignIn(Ui ui, double width)
    {
        var cardWidth = 460.0;
        var cardHeight = 230.0;
        var x = Math.Max(24, (width - cardWidth) / 2);
        var y = 90.0;

        ui.Pos(x, y);
        ui.Custom("publish-signin-card", cardWidth, cardHeight, ItemFlags.NoHit, out Item card);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, cardWidth, cardHeight, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 22, card.X + 28, card.Y + 44, "Publish a mod", Palette.Text);

        var lines = Chrome.Wrap(Fonts.Main, 14.5, "Create your page and ship releases. Your account is your author profile.", cardWidth - 56, 3);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14.5, card.X + 28, card.Y + 84 + i * 22, lines[i], Palette.Muted);

        if (ActionButton(ui, "publish-signin", "Sign in", x + 28, y + cardHeight - 66, 120, true))
            shell.SignIn();
    }

    private async Task LoadAuthors()
    {
        var authors = new List<Author> { new(shell.App.Forge.UserId ?? "", shell.User.Str("display_name") ?? "You", shell.User.Bool("is_verified")) };

        try
        {
            var groups = (await shell.App.Forge.GetJsonAsync("/api/v1/groups"))["items"] as JsonArray ?? [];

            foreach (var group in groups.OfType<JsonObject>())
            {
                var id = group.Str("id") ?? "";
                var verified = false;

                try
                {
                    verified = (await shell.App.Forge.GetJsonAsync("/api/v1/users/" + Uri.EscapeDataString(id))).Bool("is_verified");
                }
                catch (UserFacingException)
                {
                }

                authors.Add(new Author(id, group.Str("name") ?? id, verified));
            }
        }
        catch (UserFacingException)
        {
        }

        _authors = authors;
        shell.Invalidate();
    }
}
