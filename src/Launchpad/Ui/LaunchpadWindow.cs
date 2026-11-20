using GoomeSharp;
using Launchpad.App;
using Launchpad.Game;

namespace Launchpad.Ui;

using Ui = GoomeSharp.Ui;

public sealed class LaunchpadWindow
{
    public LaunchpadWindow(LaunchpadApp app, GoomeApp window)
    {
        Shell = new Shell(app, window);
        LaunchpadTheme.Apply(window.Ui.Theme);
        window.Flags = AppFlags.NoPad;
        Shell.Router.Push(new Route(GameLocator.IsGameFolder(app.Settings.GamePath) ? Page.Browse : Page.Settings));
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
        Shell.Router.Screen.Draw(ui, width, height);
    }
}
