using System.Text.Json;
using System.Text.Json.Nodes;
using Launchpad.Game;
using Launchpad.Mods;

namespace Launchpad.Ui.Screens;

public sealed partial class GameSettingsScreen
{
    private IReadOnlyList<GameInstall>? _installs;
    private LoaderInstaller.Status? _status;
    private string? _statusError;
    private string? _working;
    private string _progress = "";
    private int _request;

    private bool Active => ReferenceEquals(shell.Router.Screen, this);

    private string? GamePath => shell.App.Settings.GamePath;

    private bool GameValid => GameLocator.IsGameFolder(GamePath);

    private ModLoader Loader => shell.App.Settings.Loader;

    private void Scan()
    {
        _installs = null;
        shell.Invalidate();

        _ = Task.Run(() =>
        {
            _installs = GameLocator.Discover();
            shell.Invalidate();
        });
    }

    private void LoadStatus()
    {
        var request = ++_request;
        _status = null;
        _statusError = null;

        if (!GameValid)
            return;

        _ = Task.Run(async () =>
        {
            LoaderInstaller.Status? status = null;
            string? error = null;

            try
            {
                status = await LoaderInstaller.StatusAsync(GamePath!, Loader);
            }
            catch (Infrastructure.UserFacingException ex)
            {
                error = ex.Message;
            }

            if (request != _request)
                return;

            _status = status;
            _statusError = error;
            shell.Invalidate();
        });
    }

    private void OnAppEvent(string name, object? data)
    {
        if (name == "progress" && JsonSerializer.SerializeToNode(data) is JsonNode node)
            _progress = node["message"]?.GetValue<string>() ?? "";

        if (name == "mods" && Active)
            LoadStatus();

        shell.Invalidate();
    }

    private void Work(string name, string method, JsonObject args, string done)
    {
        _working = name;
        _progress = "";
        shell.Invalidate();

        _ = Task.Run(async () =>
        {
            var worked = await shell.CallAsync(method, args);
            _working = null;
            LoadStatus();

            if (worked)
                shell.Toast(done);
        });
    }

    private void SelectGame(string path)
    {
        Work("game", "game.select", new JsonObject { ["path"] = path }, "Game folder saved.");
    }

}
