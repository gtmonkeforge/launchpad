using System.Diagnostics;
using Launchpad.Forge;
using Launchpad.Game;
using Launchpad.Infrastructure;
using Launchpad.Injector;
using Launchpad.Mods;
using Launchpad.Security;

namespace Launchpad.App;

public sealed class LaunchpadApp : IDisposable
{
    private LaunchpadApp(Settings settings)
    {
        Settings = settings;
        Secrets = SecretStore.Create();
        Forge = new ForgeClient(Secrets);
        Devices = new DeviceRegistry(Forge, new DeviceKeyStore(Secrets), AppPaths.Devices);
        Payloads = new PayloadClient(Forge, Devices);
        Loaders = new LoaderInstaller(LoaderInstaller.CreateHttp(), message => Emit("progress", new { message }));
        Mods = new ModManager(Forge, Payloads, Loaders, () => Settings, message => Emit("progress", new { message }));
        Injector = new InjectorHost(Payloads, () => Settings.GamePath);

        Forge.SessionChanged += () => Emit("session", new { user = Forge.User });
        Mods.Changed += () => Emit("mods", null);
        Injector.Status += (message, warning) => Emit("injector", new { message, warning });
    }

    public Settings Settings { get; }
    public ISecretStore Secrets { get; }
    public ForgeClient Forge { get; }
    public DeviceRegistry Devices { get; }
    public PayloadClient Payloads { get; }
    public LoaderInstaller Loaders { get; }
    public ModManager Mods { get; }
    public InjectorHost Injector { get; }

    public event Action<string, object?>? Event;

    public void Emit(string name, object? data) => Event?.Invoke(name, data);

    public static async Task<LaunchpadApp> CreateAsync()
    {
        Settings settings;

        try
        {
            settings = await Settings.LoadAsync();
        }
        catch (UserFacingException)
        {
            settings = new Settings();
        }

        if (!GameLocator.IsGameFolder(settings.GamePath))
        {
            IReadOnlyList<GameInstall> found = GameLocator.Discover();

            if (found.Count == 1)
            {
                settings.GamePath = found[0].Path;
                await settings.SaveAsync();
            }
        }

        var app = new LaunchpadApp(settings);
        app.Injector.Start();
        return app;
    }

    public static void OpenExternal(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new UserFacingException("Launchpad only opens web links.");

        ProcessStartInfo start = OperatingSystem.IsWindows()
            ? new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }
            : new ProcessStartInfo("xdg-open") { ArgumentList = { uri.AbsoluteUri }, UseShellExecute = false };

        using Process? _ = Process.Start(start);
    }

    public void Dispose()
    {
        Injector.Dispose();
        Forge.Dispose();
    }
}
