using GoomeSharp;
using Launchpad.App;

namespace Launchpad.Ui;

public sealed class LaunchpadWindow
{
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

        using var window = new GoomeApp(options) { Flags = AppFlags.NoPad };
        LaunchpadTheme.Apply(window.Ui.Theme);
        window.Run(ui => ui.Label("Launchpad"));
    }
}
