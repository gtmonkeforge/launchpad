using System.Text.Json.Nodes;
using Launchpad.Game;
using Launchpad.Infrastructure;
using Launchpad.Mods;

namespace Launchpad.Ui.Screens;

public sealed partial class ModScreen
{
    private JsonNode? _mod;
    private string? _error;
    private bool _loading;
    private int _request;
    private InstalledView? _record;
    private bool _working;
    private bool _confirmRemove;
    private string? _result;
    private string? _failure;

    private JsonNode? Current
    {
        get
        {
            if (_mod?["releases"] is not JsonArray releases)
                return null;

            var version = _mod.Str("current_version");
            return releases.FirstOrDefault(release => release.Str("version") == version) ?? releases.FirstOrDefault();
        }
    }

    private bool Locked => _mod.Bool("gated") && !_mod.Bool("owned");

    private bool GameValid => GameLocator.IsGameFolder(_shell.App.Settings.GamePath);

    private void Load()
    {
        var request = ++_request;
        _loading = true;
        _error = null;
        _shell.Invalidate();

        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode mod = await _shell.App.Forge.GetJsonAsync(ModManager.ModPath(_guid, _channel));

                if (request != _request)
                    return;

                _mod = mod;
                _layout = null;
                _shot = 0;
            }
            catch (UserFacingException ex)
            {
                if (request == _request)
                    _error = ex.Message;
            }

            if (request == _request)
                _loading = false;

            LoadInstalled();
        });
    }

    private void LoadInstalled()
    {
        _ = Task.Run(async () =>
        {
            var all = await _shell.App.Mods.ListAsync();
            _record = all.FirstOrDefault(mod => mod.Guid == _guid || mod.Guid == _mod.Str("guid"));
            _shell.Invalidate();
        });
    }

    private void Install()
    {
        _working = true;
        _result = null;
        _failure = null;
        var name = _mod.Str("name");
        _shell.Call("mods.install", new JsonObject { ["guid"] = _mod.Str("guid"), ["channel"] = _channel }, done =>
        {
            _working = false;
            _result = done is InstalledView { Injector: true }
                ? $"{name} is ready. It loads the next time you start Gorilla Tag with Launchpad open."
                : $"{name} v{(done as InstalledView)?.Version} is installed.";
        }, message =>
        {
            _working = false;
            _failure = message;
        });
    }

    private void Remove()
    {
        _working = true;
        _confirmRemove = false;
        _result = null;
        _failure = null;
        var name = _mod.Str("name");
        _shell.Call("mods.uninstall", new JsonObject { ["guid"] = _mod.Str("guid") }, done =>
        {
            _working = false;
            _result = $"{name} was removed.";
        }, message =>
        {
            _working = false;
            _failure = message;
        });
    }

    private void SwitchChannel(string channel)
    {
        _channel = channel;
        _result = null;
        _failure = null;
        Load();
    }
}
