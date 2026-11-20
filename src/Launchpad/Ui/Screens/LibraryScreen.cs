using GoomeSharp;
using Launchpad.Game;
using System.Text.Json.Nodes;
using Launchpad.Infrastructure;
using Launchpad.Mods;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class LibraryScreen : IScreen
{
    private const double SideMargin = 30;
    private const double RightGutter = 57;
    private const double RowHeight = 76;

    private readonly Shell shell;
    private InstalledView? _confirming;
    private bool _confirmOpen;
    private bool _started;
    private double _ox;
    private double _oy;

    public LibraryScreen(Shell shell)
    {
        this.shell = shell;
        shell.App.Mods.Changed += () =>
        {
            if (Active)
                Load(false);
        };
        shell.App.Forge.SessionChanged += () =>
        {
            if (Active)
                Load(false);
        };
    }

    public void Reload() => Load(true);

    public void Draw(Ui ui, double width, double height)
    {
        if (!_started)
        {
            _started = true;
            Load(true);
        }

        ui.ScrollBegin("library", height);
        ui.Pos(0, 0);
        ui.Custom("library-origin", 1, 1, ItemFlags.NoHit, out Item origin);
        _ox = origin.X;
        _oy = origin.Y;
        var bottom = GameLocator.IsGameFolder(shell.App.Settings.GamePath) ? DrawLibrary(ui, width) : DrawNoGame(ui, width);
        ui.Pos(0, bottom + 40);
        ui.Custom("library-end", 1, 1, ItemFlags.NoHit, out _);
        ui.ScrollEnd();
        DrawConfirm(ui);
    }

    private double DrawNoGame(Ui ui, double width)
    {
        Widgets.PageHead(ui.Canvas()!, _ox + SideMargin, _oy + 27, "Library");
        var wanted = Widgets.EmptyState(ui, "library-nogame", SideMargin, 100, width - SideMargin - RightGutter, "Choose your game folder",
            "Launchpad finds Gorilla Tag in Steam (SteamVR) and Meta Horizon Link libraries automatically, or you can pick the folder yourself.", "Choose game folder");

        if (wanted)
            shell.Router.Push(new Route(Page.Game));

        return 270;
    }

    private double DrawLibrary(Ui ui, double width)
    {
        var left = SideMargin;
        var inner = width - SideMargin - RightGutter;
        DrawHead(ui, width, inner);
        var y = 112.0;

        if (_gameRunning)
        {
            var text = "Gorilla Tag is running. Close it before installing or removing mods that use files.";
            Widgets.Notice(ui.Canvas()!, _ox + left, _oy + y, inner, text);
            y += Widgets.NoticeHeight(inner, text) + 16;
        }

        y = DrawInstalled(ui, left, y, inner);
        return DrawPurchases(ui, left, y + 32, inner);
    }

    private void DrawHead(Ui ui, double width, double inner)
    {
        var game = shell.App.Settings;
        var label = GameLocator.Classify(game.GamePath!) switch
        {
            GamePlatform.Steam => "Steam (SteamVR)",
            GamePlatform.Meta => "Meta Horizon Link",
            _ => "Custom folder",
        };

        Canvas c = ui.Canvas()!;
        Widgets.PageHead(c, _ox + SideMargin, _oy + 27, "Library");
        var x = _ox + SideMargin;
        x += Chrome.Text(c, Fonts.Main, 14.5, x, _oy + 78, label + " · ", Palette.Muted);
        var path = Chrome.Clip(Fonts.Mono, 13.5, game.GamePath!, Math.Max(120, inner - 220 - (x - _ox - SideMargin)));
        Chrome.Text(c, Fonts.Mono, 13.5, x, _oy + 78, path, Palette.Muted);

        var right = width - RightGutter;
        var updatable = Updatable.Count();

        if (updatable >= 2)
        {
            var label2 = "Update all";
            var buttonWidth = Fonts.Main.Measure(14, label2) + 30;
            right -= buttonWidth;

            if (Widgets.Button(ui, "library-update-all", right, 29, label2, ButtonKind.Primary, out _, _busy.Count == 0))
                UpdateAll();

            right -= 10;
        }

        var check = _loading ? "Checking..." : "Check for updates";
        right -= Fonts.Main.Measure(14, check) + 30;

        if (Widgets.Button(ui, "library-refresh", right, 29, check, ButtonKind.Subtle, out _, !_loading))
            Load(true);
    }

    private double DrawInstalled(Ui ui, double left, double y, double inner)
    {
        if (_mods.Count == 0 && _loading)
        {
            ui.Pos(left + inner / 2 - 12, y + 50);
            ui.Spinner(24);
            return y + 110;
        }

        if (_mods.Count == 0)
        {
            if (Widgets.EmptyState(ui, "library-empty", left, y, inner, "No mods installed yet", "Find something you like and install it from its page.", "Browse mods"))
                shell.Router.Push(new Route(Page.Browse));

            return y + 170;
        }

        foreach (InstalledView mod in _mods)
        {
            DrawRow(ui, mod, left, y, inner);
            y += RowHeight + 10;
        }

        return y;
    }

    private void DrawRow(Ui ui, InstalledView mod, double left, double y, double inner)
    {
        var detail = _details.GetValueOrDefault(mod.Guid);
        var name = detail.Str("name") ?? mod.Name;
        var busy = _busy.Contains(mod.Guid);

        ui.Pos(left, y);
        ui.Custom("library-row-" + mod.Guid, inner, RowHeight, ItemFlags.NoHit, out Item row);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, row.X, row.Y, inner, RowHeight, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Widgets.Tile(c, shell, row.X + 15, row.Y + 15, 46, name, detail.Str("logo"));

        var nameWidth = Fonts.Main.Measure(16, name);
        Item title = Chrome.Hit(ui, "library-name-" + mod.Guid, left + 76, y + 11, nameWidth, 24);
        c = ui.Canvas()!;
        Chrome.BoldText(c, Fonts.Main, 16, title.X, title.Y + 12, name, title.Hovered ? Palette.Accent : Palette.Text);

        if (title.Clicked)
            shell.Router.Push(new Route(Page.Mod, mod.Guid));

        DrawPills(c, mod, row.X + 76, row.Y + 42);
        DrawRowButtons(ui, mod, name, left + inner - 15, y + (RowHeight - 34) / 2, busy);
    }

    private void DrawPills(Canvas c, InstalledView mod, double x, double y)
    {
        x += Widgets.Pill(c, x, y, "v" + mod.Version, Palette.Muted, true) + 5;
        x += Widgets.Pill(c, x, y, mod.Loader == "melonloader" ? "MelonLoader" : "BepInEx 5", Palette.Muted) + 5;

        if (mod.Channel != "stable")
            x += Widgets.Pill(c, x, y, mod.Channel, Palette.Accent, true) + 5;

        if (mod.Injector)
            x += Widgets.Pill(c, x, y, "Injector", Palette.Muted) + 5;

        if (Outdated(mod))
            x += Widgets.Pill(c, x, y, "v" + Latest(mod) + " available", Palette.Warn) + 5;

        if (Inactive(mod))
            Widgets.Pill(c, x, y, "Inactive loader", Palette.Bad);
    }

    private void DrawRowButtons(Ui ui, InstalledView mod, string name, double right, double y, bool busy)
    {
        var remove = busy ? "Working..." : "Remove";
        right -= Fonts.Main.Measure(14, remove) + 30;

        if (Widgets.Button(ui, "library-remove-" + mod.Guid, right, y, remove, ButtonKind.Danger, out _, !busy))
        {
            _confirming = mod;
            _confirmOpen = true;
        }

        if (!Outdated(mod) || Inactive(mod) || busy)
            return;

        right -= Fonts.Main.Measure(14, "Update") + 30 + 8;

        if (Widgets.Button(ui, "library-update-" + mod.Guid, right, y, "Update", ButtonKind.Primary, out _))
            Install(mod.Guid, mod.Channel, name, true);
    }

    private void DrawConfirm(Ui ui)
    {
        if (!ui.ModalBegin("Remove this mod?", ref _confirmOpen))
            return;

        ui.Wrapped($"Remove {_confirming?.Name} from Gorilla Tag?");
        ui.Space(6);

        if (ui.Button("Remove") && _confirming != null)
        {
            Remove(_confirming);
            ui.PopupClose();
        }

        ui.SameLine();

        if (ui.Button("Keep it"))
            ui.PopupClose();

        ui.ModalEnd();
    }
}
