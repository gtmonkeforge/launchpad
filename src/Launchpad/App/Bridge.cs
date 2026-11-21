using System.Text.Json.Nodes;

using Launchpad.Forge;
using Launchpad.Game;
using Launchpad.Infrastructure;
using Launchpad.Mods;

namespace Launchpad.App;

public sealed class Bridge(LaunchpadApp app, Func<string, Task<string?>> pickFolder, Action quitForUpdate)
{
    private CancellationTokenSource? _signIn;

    public async Task<object?> DispatchAsync(string method, JsonObject args, CancellationToken ct)
    {
        string Arg(string name) => args.Str(name) ?? throw new UserFacingException($"Missing {name}.");

        switch (method)
        {
            case "app.state":
                return await StateAsync();

            case "auth.signIn":
                return await SignInAsync(ct);

            case "auth.cancel":
                _signIn?.Cancel();
                return null;

            case "auth.signOut":
                await app.Forge.LogoutAsync(ct);
                return null;

            case "update.state":
                return app.Updates.State;

            case "update.check":
                return await app.Updates.CheckAsync(false, ct);

            case "update.install":
                if (!app.Updates.LaunchInstaller(relaunch: true))
                    throw new UserFacingException("There’s no downloaded update to install. Check for updates and try again.");

                quitForUpdate();
                return null;

            case "update.setAuto":
                app.Settings.AutoUpdate = args.Bool("enabled");
                await app.Settings.SaveAsync();
                return new { auto_update = app.Settings.AutoUpdate };

            case "shell.open":
                LaunchpadApp.OpenExternal(Arg("url"));
                return null;

            case "shell.openForge":
                LaunchpadApp.OpenExternal(app.Forge.Resolve(Arg("path")).AbsoluteUri);
                return null;

            case "game.detect":
                return GameLocator.Discover().Select(install => new { platform = install.Platform, label = install.Label, path = install.Path });

            case "game.browse":
                return await pickFolder("Choose your Gorilla Tag folder");

            case "game.select":
            {
                var path = Arg("path");

                if (!GameLocator.IsGameFolder(path))
                    throw new UserFacingException($"That folder doesn't contain {GameLocator.Executable}.");

                app.Settings.GamePath = Path.GetFullPath(path);
                await app.Settings.SaveAsync();
                app.Injector.Publish();
                app.Emit("mods", null);
                return await StateAsync();
            }

            case "loader.set":
            {
                ModLoader loader = Loaders.Parse(Arg("loader")) ?? throw new UserFacingException("Choose BepInEx 5 or MelonLoader.");

                if (GameLocator.IsGameFolder(app.Settings.GamePath) && loader.RuntimeInstalled(app.Settings.GamePath!))
                {
                    if (GameProcess.IsRunning())
                        throw new UserFacingException("Close Gorilla Tag before switching mod loaders.");

                    LoaderInstaller.Activate(app.Settings.GamePath!, loader);
                }

                app.Settings.Loader = loader;
                await app.Settings.SaveAsync();
                app.Emit("mods", null);
                return await StateAsync();
            }

            case "loader.install":
            {
                var game = app.Mods.Game();

                if (GameProcess.IsRunning())
                    throw new UserFacingException("Close Gorilla Tag before installing the mod loader.");

                ModLoader loader = app.Settings.Loader;
                var version = await app.Loaders.InstallRuntimeAsync(game, loader, ct);
                LoaderInstaller.Activate(game, loader);

                if ((await Manifest.ReadAsync(game)).Installed.Any(mod => mod.Injector && mod.EffectiveLoader == loader))
                    await app.Loaders.DeployInjectorAsync(game, loader);

                return new { version };
            }

            case "proton.configure":
            {
                if (!OperatingSystem.IsLinux())
                    throw new UserFacingException("Proton setup is only needed on Linux.");

                Proton.Result? result = LoaderInstaller.ConfigureProton(app.Settings.Loader);
                return new { accounts = result?.Accounts ?? 0, updated = result?.Updated ?? 0 };
            }

            case "mods.installed":
                return await app.Mods.ListAsync();

            case "mods.install":
                return await app.Mods.InstallAsync(Arg("guid"), args.Str("channel"), ct);

            case "mods.installFile":
            {
                var data = Arg("data");

                if (data.Length > (ModManager.MaxCustomBytes + 2) / 3 * 4)
                    throw new UserFacingException("That file is too large to install.");

                byte[] bytes;

                try
                {
                    bytes = Convert.FromBase64String(data);
                }
                catch (FormatException)
                {
                    throw new UserFacingException("Launchpad couldn't read that file. Try again.");
                }

                return await app.Mods.InstallFileAsync(Arg("name"), bytes, ct);
            }

            case "mods.uninstall":
                return await app.Mods.UninstallAsync(Arg("guid"));

            case "mods.updates":
                return await app.Mods.CheckUpdatesAsync(ct);

            default:
                throw new UserFacingException("Unknown action: " + method);
        }
    }

    private async Task<object> SignInAsync(CancellationToken ct)
    {
        _signIn?.Cancel();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _signIn = cancel;
        using OAuthSignIn flow = app.Forge.StartSignIn();

        try
        {
            LaunchpadApp.OpenExternal(flow.SignInUrl.AbsoluteUri);
            var code = await flow.WaitForCodeAsync(cancel.Token);
            await app.Forge.ExchangeAsync(code, flow.Verifier, flow.RedirectUri, cancel.Token);
            return new { user = app.Forge.User };
        }
        catch (OperationCanceledException)
        {
            throw new UserFacingException("Sign-in was cancelled.");
        }
        finally
        {
            if (ReferenceEquals(_signIn, cancel))
                _signIn = null;
        }
    }

    public async Task<object> StateAsync()
    {
        var game = app.Settings.GamePath;
        var valid = GameLocator.IsGameFolder(game);
        object? loader = null;
        string? loaderError = null;

        if (valid)
        {
            try
            {
                LoaderInstaller.Status status = await LoaderInstaller.StatusAsync(game!, app.Settings.Loader);
                loader = new { runtime_installed = status.RuntimeInstalled, active = status.Active, version = status.Version, injector_deployed = status.InjectorDeployed };
            }
            catch (UserFacingException ex)
            {
                loaderError = ex.Message;
            }
        }

        var device = await app.Devices.CurrentAsync();
        GamePlatform? platform = valid ? GameLocator.Classify(game!) : null;

        return new
        {
            version = typeof(Bridge).Assembly.GetName().Version?.ToString(3) ?? "2.0.0",
            os = OperatingSystem.IsWindows() ? "windows" : "linux",
            server = app.Forge.BaseUri.GetLeftPart(UriPartial.Authority),
            user = app.Forge.User,
            restore_error = app.Forge.RestoreError,
            update = app.Updates.State,
            auto_update = app.Settings.AutoUpdate,
            settings = new { loader = app.Settings.Loader.Id(), accent = app.Settings.Accent, game_path = game },
            game = new
            {
                path = game,
                valid,
                platform,
                label = platform switch
                {
                    GamePlatform.Steam => "Steam (SteamVR)",
                    GamePlatform.Meta => "Meta Horizon Link",
                    GamePlatform.Custom => "Custom folder",
                    _ => null,
                },
                running = GameProcess.IsRunning(),
            },
            loader,
            loader_error = loaderError,
            secrets = new { description = app.Secrets.Description, @protected = app.Secrets.Protected },
            device = device == null ? null : new { id = device.DeviceId, name = device.Name, storage = device.Storage },
        };
    }
}
