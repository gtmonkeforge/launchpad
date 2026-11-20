using Launchpad.App;
using Launchpad.Ui;

namespace Launchpad;

public static class Program
{
    [STAThread]
    public static int Main()
    {
        using var single = new Mutex(true, "MonkeForge.Launchpad." + Environment.UserName, out var first);

        if (!first)
        {
            Console.Error.WriteLine("Launchpad is already running");
            return 1;
        }

        using LaunchpadApp app = LaunchpadApp.CreateAsync().GetAwaiter().GetResult();
        LaunchpadWindow.Run(app);
        return 0;
    }
}
