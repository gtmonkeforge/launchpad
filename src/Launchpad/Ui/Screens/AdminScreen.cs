using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed class AdminScreen(Shell shell) : IScreen
{
    private const double CardWidth = 480;

    private JsonNode? _facts;
    private string? _error;
    private bool _asked;

    public void Draw(Ui ui, double width, double height)
    {
        if (shell.SignedIn && shell.IsAdmin && !_asked)
            Ask();

        ui.ScrollBegin("admin", height);
        ui.Pos(0, 0);
        ui.Custom("admin-origin", 1, 1, ItemFlags.NoHit, out Item origin);
        var bottom = shell.SignedIn && shell.IsAdmin ? DrawFacts(ui, width, origin) : DrawGuest(ui, width, height);
        shell.Footer.Finish(ui, "admin-end", bottom, height, width);
        ui.ScrollEnd();
    }

    public void Reload() => _asked = false;

    private double DrawGuest(Ui ui, double width, double height)
    {
        var text = shell.SignedIn ? "This account isn't staff, so there's nothing to moderate." : "Sign in with a staff account to moderate mods, users and reports.";
        var cardHeight = 116 + Card.HintHeight(text, CardWidth);
        var x = Math.Max(24, (width - CardWidth) / 2);
        var y = Math.Max(24, (height - Footer.Height - cardHeight) / 3);

        Card card = Card.Open(ui, "admin-card", x, y, CardWidth, cardHeight, "Admin");
        card.Hint(0, 46, CardWidth, text);

        if (Card.Button(ui, shell.SignedIn ? "Back to mods" : "Sign in", x + Card.Padding, y + cardHeight - 56, 360))
            shell.Router.Push(new Route(shell.SignedIn ? Page.Browse : Page.SignIn));

        return y + cardHeight;
    }

    private double DrawFacts(Ui ui, double width, Item origin)
    {
        var left = SettingsNav.SideMargin;
        var inner = Math.Min(900, width - left - SettingsNav.RightMargin);
        Widgets.PageHead(ui.Canvas()!, origin.X + left, origin.Y + 27, "Admin");
        var y = 100.0;

        if (_error != null)
        {
            Card failed = Card.Open(ui, "admin-error", left, y, inner, 100, "Couldn't load the numbers");
            failed.Hint(0, 46, inner, _error, Palette.Bad);
            y += 116;
        }
        else if (_facts == null)
        {
            ui.Pos(left + inner / 2 - 12, y + 40);
            ui.Spinner(24);
            y += 100;
        }
        else
            y = DrawTiles(ui, left, y, inner);

        const string hint = "Reviews, reports and users are handled in the admin panel on monkeforge.org.";
        var height = 112 + Card.HintHeight(hint, inner);
        Card panel = Card.Open(ui, "admin-panel", left, y, inner, height, "Admin panel");
        panel.Hint(0, 46, inner, hint);

        if (Widgets.Button(ui, "admin-open", left + Card.Padding, y + height - 56, "Open the admin panel on monkeforge.org", ButtonKind.Primary, out _, true, 36, Glyphs.Link))
            shell.OpenForge("/admin");

        return y + height;
    }

    private double DrawTiles(Ui ui, double left, double y, double inner)
    {
        var tiles = new (string Label, long Value, bool Alert)[]
        {
            ("Mods waiting for review", _facts["reviews"].Long("pending"), true),
            ("Open support tickets", _facts["tickets"].Long("open"), true),
            ("Unread tickets", _facts["tickets"].Long("unread"), true),
            ("Open disputes", _facts.Long("disputes_open"), true),
            ("Errors today", _facts.Long("errors_day"), true),
            ("Users", _facts["users"].Long("total"), false),
            ("New users this week", _facts["users"].Long("new_week"), false),
            ("Downloads this week", _facts.Long("downloads_week"), false),
        };

        var columns = inner >= 760 ? 4 : 2;
        var cell = (inner - 12 * (columns - 1)) / columns;

        for (var i = 0; i < tiles.Length; i++)
        {
            var x = left + i % columns * (cell + 12);
            var top = y + i / columns * 82;
            Card tile = Card.Open(ui, "admin-tile-" + i, x, top, cell, 70, "");
            tile.Text(16, 24, 13, Chrome.Clip(Fonts.Main, 13, tiles[i].Label, cell - 32), Palette.Muted);
            tile.Text(16, 48, 18, Format.Number(tiles[i].Value), tiles[i].Alert && tiles[i].Value > 0 ? Palette.Warn : Palette.Text, true);
        }

        return y + (int)Math.Ceiling(tiles.Length / (double)columns) * 82 + 4;
    }

    private void Ask()
    {
        _asked = true;
        _facts = null;
        _error = null;

        _ = Task.Run(async () =>
        {
            try
            {
                _facts = await shell.App.Forge.GetJsonAsync("/api/v1/admin/dashboard");
            }
            catch (UserFacingException ex)
            {
                _error = ex.Message;
            }

            shell.Invalidate();
        });
    }
}
