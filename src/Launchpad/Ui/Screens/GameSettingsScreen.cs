using GoomeSharp;
using Launchpad.Game;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class GameSettingsScreen : IScreen
{
    private const double SideMargin = 30;
    private const double RightGutter = 57;
    private const double MaxWidth = 780;
    private const double PanelGap = 18;

    private readonly Shell shell;
    private bool _started;
    private double _ox;
    private double _oy;

    public GameSettingsScreen(Shell shell)
    {
        this.shell = shell;
        shell.App.Event += OnAppEvent;
    }

    public void Reload()
    {
        Scan();
        LoadStatus();
    }

    public void Draw(Ui ui, double width, double height)
    {
        if (!_started)
        {
            _started = true;
            Reload();
        }

        ui.ScrollBegin("game-settings", height);
        ui.Pos(0, 0);
        ui.Custom("game-origin", 1, 1, ItemFlags.NoHit, out Item origin);
        _ox = origin.X;
        _oy = origin.Y;

        var panel = Math.Min(MaxWidth, width - SideMargin - RightGutter);
        Canvas c = ui.Canvas()!;
        Widgets.PageHead(c, _ox + SideMargin, _oy + 27, "Game & mod loader");
        Chrome.Text(c, Fonts.Main, 14.5, _ox + SideMargin, _oy + 78, "These settings are stored on this computer.", Palette.Muted);

        var y = 112.0;
        y = DrawGame(ui, SideMargin, y, panel) + PanelGap;
        y = DrawInstalls(ui, SideMargin, y, panel) + PanelGap;
        y = DrawLoader(ui, SideMargin, y, panel) + PanelGap;

        if (OperatingSystem.IsLinux())
            y = DrawProton(ui, SideMargin, y, panel) + PanelGap;

        y = DrawAbout(ui, SideMargin, y, panel);

        ui.Pos(0, y + 40);
        ui.Custom("game-end", 1, 1, ItemFlags.NoHit, out _);
        ui.ScrollEnd();
        DrawFolderDialog(ui);
    }

    private void Panel(Ui ui, string id, double x, double y, double width, double height, string title)
    {
        ui.Pos(x, y);
        ui.Custom(id, width, height, ItemFlags.NoHit, out Item box);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 17, box.X + 22, box.Y + 28, title, Palette.Text);
    }

    private void Hint(double x, double y, double width, string text)
    {
        Canvas c = shell.Window.Ui.Canvas()!;
        var lines = Chrome.Wrap(Fonts.Main, 14, text, width - 44, 4);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14, _ox + x + 22, _oy + y + i * 21, lines[i], Palette.Muted);
    }

    private static int HintLines(string text, double width) => Chrome.Wrap(Fonts.Main, 14, text, width - 44, 4).Count;

    private double DrawGame(Ui ui, double x, double y, double width)
    {
        var notice = $"Launchpad hasn't found Gorilla Tag yet. Pick one of the installs below, or choose the folder that contains {GameLocator.Executable}.";
        var height = 52 + (GameValid ? 54 : Widgets.NoticeHeight(width - 44, notice)) + 18 + 34 + 22;
        Panel(ui, "game-panel", x, y, width, height, "Gorilla Tag");
        Canvas c = ui.Canvas()!;

        if (GameValid)
        {
            var label = GameLocator.Classify(GamePath!) switch
            {
                GamePlatform.Steam => "Steam (SteamVR)",
                GamePlatform.Meta => "Meta Horizon Link",
                _ => "Custom folder",
            };
            InstallBox(c, _ox + x + 22, _oy + y + 52, width - 44, label, GamePath!, true);
        }
        else
        {
            Widgets.Notice(c, _ox + x + 22, _oy + y + 52, width - 44, notice);
        }

        if (Widgets.Button(ui, "game-choose-folder", x + 22, y + height - 56, "Choose folder...", ButtonKind.Subtle, out _))
            OpenFolderDialog();

        return y + height;
    }

    private static void InstallBox(Canvas c, double x, double y, double width, string label, string path, bool selected)
    {
        Chrome.Box(c, x, y, width, 54, Palette.Radius, Palette.Surface2, selected ? Palette.AccentLine : Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 14.5, x + 16, y + 19, label, Palette.Text);
        Chrome.Text(c, Fonts.Mono, 12.5, x + 16, y + 38, Chrome.Clip(Fonts.Mono, 12.5, path, width - 140), Palette.Muted);

        if (selected)
            Widgets.Pill(c, x + width - 16 - Fonts.Main.Measure(12.5, "Selected") - 18, y + 15, "Selected", Palette.Good);
    }

    private double DrawInstalls(Ui ui, double x, double y, double width)
    {
        var rows = Math.Max(1, _installs?.Count ?? 1);
        var height = 96 + rows * 62;
        Panel(ui, "installs-panel", x, y, width, height, "Detected installs");

        var scan = _installs == null ? "Scanning..." : "Scan again";
        var scanWidth = Fonts.Main.Measure(14, scan) + 30;

        if (Widgets.Button(ui, "installs-rescan", x + width - 22 - scanWidth, y + 12, scan, ButtonKind.Subtle, out _, _installs != null, 32))
            Scan();

        var where = OperatingSystem.IsWindows() ? "every Steam library (SteamVR) and every Meta Horizon Link library" : "every Steam library (SteamVR)";
        Hint(x, y + 56, width, $"Launchpad looks in {where} on this computer.");
        var top = y + 84;

        if (_installs == null)
            return y + height;

        if (_installs.Count == 0)
        {
            Chrome.Text(ui.Canvas()!, Fonts.Main, 14.5, _ox + x + 22, _oy + top + 20, "No installs found in Steam or Meta Horizon Link libraries.", Palette.Faint);
            return y + height;
        }

        foreach (GameInstall install in _installs)
        {
            var selected = Same(install.Path, GamePath);
            ui.Pos(x + 22, top);
            ui.Custom("install-" + install.Path, width - 44, 54, ItemFlags.NoHit, out Item box);
            InstallBox(ui.Canvas()!, box.X, box.Y, width - 44, install.Label, install.Path, selected);

            if (!selected && Widgets.Button(ui, "install-use-" + install.Path, x + width - 22 - 96, top + 10, "Use this", ButtonKind.Primary, out _, _working == null))
                SelectGame(install.Path);

            top += 62;
        }

        return y + height;
    }

    private static bool Same(string a, string? b)
    {
        return b != null && string.Equals(Path.GetFullPath(a).TrimEnd('/', '\\'), Path.GetFullPath(b).TrimEnd('/', '\\'), StringComparison.Ordinal);
    }
}
