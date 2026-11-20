using GoomeSharp;
using Launchpad.Mods;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class GameSettingsScreen
{
    private const string LoaderHint = "Mods are installed for one loader at a time. Switching keeps both loaders' mods; only the chosen one runs.";

    private double DrawLoader(Ui ui, double x, double y, double width)
    {
        var hintLines = HintLines(LoaderHint, width);
        var segmentTop = 56 + hintLines * 21 + 6;
        var installHint = $"{Loader.DisplayName()} is installed automatically with your first mod. Use this to reinstall it or get the newest release from its official GitHub page.";
        var installLines = HintLines(installHint, width);
        var height = segmentTop + 36 + 28 + (GameValid ? 160 + installLines * 21 : 60);

        Panel(ui, "loader-panel", x, y, width, height, "Mod loader");
        Hint(x, y + 56, width, LoaderHint);
        Segment(ui, x + 22, y + segmentTop, ModLoader.Bepinex5, 0);
        Segment(ui, x + 22, y + segmentTop, ModLoader.MelonLoader, 1);

        var top = y + segmentTop + 36 + 28;
        Canvas c = ui.Canvas()!;
        Chrome.BoldText(c, Fonts.Main, 15, _ox + x + 22, _oy + top, "Status", Palette.Text);
        top += 18;

        if (!GameValid)
        {
            Chrome.Text(c, Fonts.Main, 14.5, _ox + x + 22, _oy + top + 14, "Choose your game folder first.", Palette.Muted);
            return y + height;
        }

        if (_statusError != null)
            Chrome.Text(c, Fonts.Main, 14.5, _ox + x + 22, _oy + top + 14, Chrome.Clip(Fonts.Main, 14.5, _statusError, width - 44), Palette.Bad);
        else
            StatusGrid(c, x + 22, top, width - 44);

        Hint(x, top + 80, width, installHint);
        DrawInstallButton(ui, x + 22, top + 80 + installLines * 21 + 6);
        return y + height;
    }

    private void Segment(Ui ui, double x, double y, ModLoader loader, int index)
    {
        var buttonWidth = 140.0;
        Item item = Chrome.Hit(ui, "loader-" + loader.Id(), x + index * buttonWidth, y, buttonWidth, 36);
        Canvas c = ui.Canvas()!;
        var selected = loader == Loader;
        c.DrawRRect(item.X, item.Y, buttonWidth, 36, 8, selected ? Palette.AccentLine : Palette.LineStrong);
        c.DrawRRect(item.X + 1, item.Y + 1, buttonWidth - 2, 34, 7, selected ? 0xFF3A2620 : item.Hovered ? Palette.Hover : Palette.Surface2);
        var name = loader.DisplayName();
        Chrome.Text(c, Fonts.Main, 14, item.X + buttonWidth / 2 - Fonts.Main.Measure(14, name) / 2, item.Y + 18, name, selected ? Palette.Text : Palette.Muted);

        if (item.Clicked && _working == null)
            SetLoader(loader);
    }

    private void StatusGrid(Canvas c, double x, double y, double width)
    {
        var runtime = _status == null ? "..." : _status.RuntimeInstalled ? "Installed" + (_status.Version != null ? " · " + _status.Version : "") : "Not installed";
        var active = _status == null ? "..." : _status.Active ? "Yes" : "No";
        var injector = _status == null ? "..." : _status.InjectorDeployed ? "Installed" : "Not needed yet";
        var cell = width / 3;
        var cells = new[] { ("Runtime", runtime), ("Active", active), ("Injector plugin", injector) };

        for (var i = 0; i < cells.Length; i++)
        {
            var left = _ox + x + i * cell;
            Chrome.Box(c, left, _oy + y + 8, cell - 10, 54, Palette.Radius, Palette.Surface2, Palette.Line);
            Chrome.Text(c, Fonts.Main, 12.5, left + 14, _oy + y + 26, cells[i].Item1, Palette.Muted);
            Chrome.BoldText(c, Fonts.Main, 14.5, left + 14, _oy + y + 47, Chrome.Clip(Fonts.Main, 14.5, cells[i].Item2, cell - 38), Palette.Text);
        }
    }

    private void DrawInstallButton(Ui ui, double x, double y)
    {
        var working = _working == "loader";
        var name = Loader.DisplayName();
        var label = working ? "Installing..." : (_status?.RuntimeInstalled == true ? "Reinstall " : "Install ") + name;

        if (Widgets.Button(ui, "loader-install", x, y, label, ButtonKind.Subtle, out var width, _working == null))
            InstallLoader();

        if (working && _progress.Length > 0)
            Chrome.Text(ui.Canvas()!, Fonts.Main, 14, _ox + x + width + 14, _oy + y + 17, _progress, Palette.Muted);
    }

    private double DrawProton(Ui ui, double x, double y, double width)
    {
        var text = "Under Proton, the loader only starts if Steam's launch options tell Wine to use it. Launchpad does this when it installs the loader. Close Steam first, or Steam may undo the change.";
        var lines = HintLines(text, width);
        var height = 56 + lines * 21 + 6 + 34 + 24;
        Panel(ui, "proton-panel", x, y, width, height, "Proton");
        Hint(x, y + 56, width, text);
        var label = _working == "proton" ? "Setting up..." : "Set up launch options";

        if (Widgets.Button(ui, "proton-setup", x + 22, y + 56 + lines * 21 + 6, label, ButtonKind.Subtle, out _, _working == null && GameValid))
            SetUpProton();

        return y + height;
    }

    private double DrawAbout(Ui ui, double x, double y, double width)
    {
        var rows = new List<(string, string)>
        {
            ("Version", typeof(Launchpad.App.Bridge).Assembly.GetName().Version?.ToString(3) ?? "2.0.0"),
            ("Server", shell.App.Forge.BaseUri.GetLeftPart(UriPartial.Authority)),
            ("Platform", OperatingSystem.IsWindows() ? "Windows" : "Linux"),
            ("Sign-in kept in", shell.App.Secrets.Description),
        };

        var note = "Launchpad is the desktop companion for MonkeForge. Community made. Not affiliated with Another Axiom.";
        var noteLines = HintLines(note, width);
        var height = 56 + rows.Count * 28 + 8 + noteLines * 21 + 14 + 32 + 24;
        Panel(ui, "about-panel", x, y, width, height, "MonkeForge Launchpad");
        Canvas c = ui.Canvas()!;
        var top = _oy + y + 66;

        foreach (var (label, value) in rows)
        {
            Chrome.Text(c, Fonts.Main, 14, _ox + x + 22, top, label, Palette.Muted);
            Chrome.Text(c, Fonts.Mono, 13.5, _ox + x + 170, top, Chrome.Clip(Fonts.Mono, 13.5, value, width - 190), Palette.Text);
            top += 28;
        }

        var noteTop = y + 56 + rows.Count * 28 + 8;
        Hint(x, noteTop + 10, width, note);
        var buttonTop = noteTop + 10 + noteLines * 21 + 6;

        if (Widgets.Button(ui, "about-site", x + 22, buttonTop, "Launchpad on MonkeForge", ButtonKind.Subtle, out var siteWidth, true, 32))
            shell.OpenForge("/software/launchpad");

        if (Widgets.Button(ui, "about-docs", x + 22 + siteWidth + 8, buttonTop, "Docs", ButtonKind.Subtle, out _, true, 32))
            shell.OpenForge("/docs");

        return y + height;
    }
}
