using GoomeSharp;
using Launchpad.App;
using Launchpad.Game;

namespace Launchpad.Ui;

using Ui = GoomeSharp.Ui;

public sealed class LaunchpadWindow
{
    private const double EdgeSize = 4;

    private readonly GoomeApp _window;
    private readonly Titlebar _titlebar;

    public LaunchpadWindow(LaunchpadApp app, GoomeApp window)
    {
        _window = window;
        Shell = new Shell(app, window);
        _titlebar = new Titlebar(Shell);
        Palette.SetAccent(app.Settings.Accent);
        LaunchpadTheme.Apply(window.Ui.Theme);
        window.Flags = AppFlags.NoPad;
        window.RedrawEveryMs = 150;
        window.OnEvent += OnEvent;
        Shell.Router.Push(new Route(GameLocator.IsGameFolder(app.Settings.GamePath) ? Page.Browse : Page.Game));
    }

    public Shell Shell { get; }

    public static void Run(LaunchpadApp app)
    {
        var options = new GoomeOptions
        {
            Title = "MonkeForge Launchpad",
            Width = 1280,
            Height = 820,
            MinWidth = 900,
            MinHeight = 600,
            WindowStyle = WindowStyle.Resizable | WindowStyle.Borderless | WindowStyle.Center,
        };

        using var window = new GoomeApp(options);
        var launchpad = new LaunchpadWindow(app, window);
        launchpad.RestoreSession();
        window.Run(launchpad.Draw);
    }

    public void RestoreSession()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Shell.App.Forge.RestoreAsync();
            }
            catch (Exception)
            {
            }

            Shell.Invalidate();
        });
    }

    public void Draw(Ui ui)
    {
        Canvas c = ui.Canvas()!;
        var width = c.Width / c.Scale;
        var height = c.Height / c.Scale;

        Shell.ShowPendingToast();
        _titlebar.Draw(ui, width);

        ui.Pos(0, Palette.TitlebarHeight);
        Shell.Router.Screen.Draw(ui, width, height - Palette.TitlebarHeight);
        DrawEdges(ui, width, height);
    }

    private void OnEvent(Event ev)
    {
        if (ev.Type == EventType.Key && ev.Key == '/' && ev.Mods == GoomeSharp.Mods.None && !_window.Ui.WantsKeyboard)
            _titlebar.FocusSearch();
    }

    private void DrawEdges(Ui ui, double width, double height)
    {
        if (_window.Goome.State != WindowState.Normal)
            return;

        Edge(ui, "edge-left", 0, 0, EdgeSize, height, Edges.Left, CursorShape.ResizeEW);
        Edge(ui, "edge-right", width - EdgeSize, 0, EdgeSize, height, Edges.Right, CursorShape.ResizeEW);
        Edge(ui, "edge-top", 0, 0, width, EdgeSize, Edges.Top, CursorShape.ResizeNS);
        Edge(ui, "edge-bottom", 0, height - EdgeSize, width, EdgeSize, Edges.Bottom, CursorShape.ResizeNS);
    }

    private void Edge(Ui ui, string id, double x, double y, double w, double h, Edges edges, CursorShape cursor)
    {
        ui.Pos(x, y);
        ui.Custom(id, w, h, ItemFlags.None, out Item item);

        if (item.Hovered)
            _window.Goome.Cursor = cursor;

        if (item.Pressed)
            _window.Goome.Drag(edges);
    }
}
