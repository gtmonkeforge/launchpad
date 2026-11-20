using System.Text.Json.Nodes;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

public sealed partial class ManageScreen
{
    private bool _started;
    private int _request;
    private JsonNode? _mod;
    private string? _loadError;
    private bool _allowed;
    private bool _isOwner;
    private JsonArray _channels = [];
    private JsonArray _versions = [];
    private JsonArray _transfers = [];
    private JsonNode? _store;
    private JsonNode? _payouts;

    private bool PersonalMod => _mod?["author"].Str("id") == _shell.App.Forge.UserId;

    private void Load()
    {
        var request = ++_request;
        _loadError = null;
        _shell.Invalidate();

        _ = Task.Run(async () =>
        {
            try
            {
                var forge = _shell.App.Forge;
                var mod = await forge.GetJsonAsync(ModUrl("?channel=stable"));
                var groups = await Quietly("/api/v1/groups");
                var group = (groups?["items"] as JsonArray)?.FirstOrDefault(g => g.Str("id") == mod["author"].Str("id"));

                if (request != _request)
                    return;

                var authorId = mod["author"].Str("id");
                _allowed = authorId == forge.UserId || group != null || _shell.IsAdmin;
                _isOwner = authorId == forge.UserId || group.Str("role") == "owner" || _shell.IsAdmin;
                _mod = mod;

                if (_allowed)
                    await LoadRest(mod);
            }
            catch (UserFacingException ex)
            {
                if (request == _request)
                    _loadError = ex.Message;
            }

            _shell.Invalidate();
        });
    }

    private async Task LoadRest(JsonNode mod)
    {
        var forge = _shell.App.Forge;
        _channels = (await forge.GetJsonAsync(ModUrl("/channels")))["items"] as JsonArray ?? [];
        _release.Channels = _channels.Where(channel => channel.Bool("enabled")).Select(channel => channel.Str("name") ?? "stable").ToArray();

        if (_release.Channels.Length == 0)
            _release.Channels = ["stable"];

        _release.CurrentVersion = mod.Str("current_version");
        _listing.Load(mod);
        FillListing(mod);
        _store = await Quietly("/api/v1/store");
        var personal = mod["author"].Str("id") == forge.UserId;
        _payouts = _store.Bool("enabled") ? await Quietly("/api/v1/payouts" + (personal ? "" : "?account=" + Uri.EscapeDataString(mod["author"].Str("id") ?? ""))) : null;
        var transfers = new JsonArray();

        foreach (var item in (await Quietly("/api/v1/mod-transfers"))?["items"] as JsonArray ?? [])
        {
            if (item.Str("guid") == mod.Str("guid"))
                transfers.Add(item!.DeepClone());
        }

        _transfers = transfers;
        FillAccess(mod);
        FillPricing(mod);
        await LoadVersions();
    }

    private async Task<JsonNode?> Quietly(string path)
    {
        try
        {
            return await _shell.App.Forge.GetJsonAsync(path);
        }
        catch (UserFacingException)
        {
            return null;
        }
    }

    private async Task Refresh()
    {
        var mod = await _shell.App.Forge.GetJsonAsync(ModUrl("?channel=stable"));
        _mod = mod;
        await LoadRest(mod);
    }

    private void OpenTab(int tab)
    {
        if (tab == 2)
            _ = Task.Run(LoadVersions);
    }
}
