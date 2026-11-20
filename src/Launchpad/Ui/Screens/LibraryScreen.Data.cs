using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Launchpad.Infrastructure;
using Launchpad.Mods;

namespace Launchpad.Ui.Screens;

public sealed partial class LibraryScreen
{
    private List<InstalledView> _mods = [];
    private readonly ConcurrentDictionary<string, JsonNode> _details = [];
    private List<JsonNode> _purchases = [];
    private string? _purchaseError;
    private bool _loading;
    private bool _gameRunning;
    private string _loader = "";
    private readonly ConcurrentDictionary<string, bool> _busy = [];
    private int _request;

    private bool Active => ReferenceEquals(shell.Router.Screen, this);

    private Dictionary<string, UpdateInfo> _updates = [];

    private string? Latest(InstalledView mod) => _updates.TryGetValue(mod.Guid, out UpdateInfo? update) ? update.Latest : null;

    private bool Outdated(InstalledView mod) => _updates.TryGetValue(mod.Guid, out UpdateInfo? update) && update.Available;

    private bool Inactive(InstalledView mod) => mod.Loader != _loader;

    private IEnumerable<InstalledView> Updatable => _mods.Where(mod => Outdated(mod) && !Inactive(mod));

    private void Load(bool fresh)
    {
        var request = ++_request;
        _loading = true;
        shell.Invalidate();

        if (fresh)
            _details.Clear();

        _ = Task.Run(async () =>
        {
            var mods = await shell.App.Mods.ListAsync();

            await Task.WhenAll(mods.Where(mod => !_details.ContainsKey(mod.Guid)).Select(async mod =>
            {
                try
                {
                    _details[mod.Guid] = await shell.App.Forge.GetJsonAsync(ModManager.ModPath(mod.Guid, mod.Channel));
                }
                catch (UserFacingException)
                {
                    _details[mod.Guid] = new JsonObject { ["name"] = mod.Name };
                }
            }));

            if (request != _request)
                return;

            var updates = new List<UpdateInfo>();

            try
            {
                updates = await shell.App.Mods.CheckUpdatesAsync(CancellationToken.None);
            }
            catch (UserFacingException)
            {
            }

            if (request != _request)
                return;

            _updates = updates.GroupBy(update => update.Guid).ToDictionary(group => group.Key, group => group.First());
            _mods = mods;
            _loader = shell.App.Settings.Loader.Id();
            _gameRunning = Game.GameProcess.IsRunning();
            await LoadPurchases();
            _loading = false;
            shell.Invalidate();
        });
    }

    private async Task LoadPurchases()
    {
        _purchaseError = null;

        if (!shell.SignedIn)
        {
            _purchases = [];
            return;
        }

        try
        {
            JsonNode result = await shell.App.Forge.GetJsonAsync("/api/v1/purchases");
            _purchases = result["items"] is JsonArray array ? array.Where(item => item != null).Select(item => item!.DeepClone()).ToList() : [];
        }
        catch (UserFacingException ex)
        {
            _purchaseError = ex.Message;
        }
    }

    private void Install(string guid, string? channel, string name, bool updating)
    {
        var args = new JsonObject { ["guid"] = guid };

        if (channel != null)
            args["channel"] = channel;

        _busy.TryAdd(guid, true);
        shell.Invalidate();

        _ = Task.Run(async () =>
        {
            var worked = await shell.CallAsync("mods.install", args);
            _busy.TryRemove(guid, out _);

            if (worked)
                shell.Toast(updating ? $"{name} was updated." : $"{name} is installed.");
        });
    }

    private void Remove(InstalledView mod)
    {
        _busy.TryAdd(mod.Guid, true);
        shell.Invalidate();

        _ = Task.Run(async () =>
        {
            var worked = await shell.CallAsync("mods.uninstall", new JsonObject { ["guid"] = mod.Guid });
            _busy.TryRemove(mod.Guid, out _);

            if (worked)
                shell.Toast($"{mod.Name} was removed.");
        });
    }

    private void UpdateAll()
    {
        var pending = Updatable.ToList();

        _ = Task.Run(async () =>
        {
            foreach (InstalledView mod in pending)
            {
                _busy.TryAdd(mod.Guid, true);
                shell.Invalidate();
                var worked = await shell.CallAsync("mods.install", new JsonObject { ["guid"] = mod.Guid, ["channel"] = mod.Channel });
                _busy.TryRemove(mod.Guid, out _);

                if (!worked)
                    return;
            }

            shell.Toast("Everything is up to date.");
        });
    }
}
