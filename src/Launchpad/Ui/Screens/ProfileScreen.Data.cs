using System.Text.Json.Nodes;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

public sealed partial class ProfileScreen
{
    private const int PageSize = 12;

    private JsonNode? _profile;
    private string? _error;
    private List<JsonNode> _mods = [];
    private int _modTotal;
    private int _modPage = 1;
    private bool _modsLoading;
    private List<JsonNode> _tiers = [];
    private List<JsonNode> _friends = [];
    private List<JsonNode> _requests = [];
    private List<JsonNode> _sent = [];
    private List<JsonNode> _purchases = [];
    private List<JsonNode> _subscriptions = [];
    private Dictionary<string, string> _staffNotes = [];
    private string? _purchaseError;
    private string? _confirmCancel;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _busy = [];
    private int _request;

    private string WantedId => _wanted == "me" ? shell.App.Forge.UserId ?? "" : _wanted;

    private void Load()
    {
        var request = ++_request;
        _error = null;
        shell.Invalidate();

        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode profile = await shell.App.Forge.GetJsonAsync("/api/v1/users/" + Uri.EscapeDataString(WantedId));

                if (request != _request)
                    return;

                _profile = profile;
                _tiers = await ListAsync($"/api/v1/users/{Uri.EscapeDataString(profile.Str("id") ?? "")}/tiers", "items");
                shell.Invalidate();
                await LoadMods(1);
                await LoadPeople();
                await LoadPurchases();
            }
            catch (UserFacingException ex)
            {
                if (request == _request)
                    _error = ex.Message;
            }

            shell.Invalidate();
        });
    }

    private async Task<List<JsonNode>> ListAsync(string path, string key)
    {
        try
        {
            JsonNode result = await shell.App.Forge.GetJsonAsync(path);
            return result[key] is JsonArray array ? array.Where(item => item != null).Select(item => item!.DeepClone()).ToList() : [];
        }
        catch (UserFacingException)
        {
            return [];
        }
    }

    private void GoToPage(int page)
    {
        _ = Task.Run(() => LoadMods(page));
    }

    private async Task LoadMods(int page)
    {
        _modsLoading = true;
        _modPage = page;
        shell.Invalidate();

        try
        {
            JsonNode result = await shell.App.Forge.GetJsonAsync($"/api/v1/mods?author_id={Uri.EscapeDataString(_profile.Str("id") ?? "")}&limit={PageSize}&page={page}");
            _mods = result["items"] is JsonArray array ? array.Where(item => item != null).Select(item => item!.DeepClone()).ToList() : [];
            _modTotal = (int)result.Long("total");
        }
        catch (UserFacingException)
        {
            _mods = [];
        }

        _modsLoading = false;
        shell.Invalidate();
    }

    private async Task LoadPeople()
    {
        if (!shell.SignedIn)
            return;

        try
        {
            JsonNode result = await shell.App.Forge.GetJsonAsync("/api/v1/friends");
            _friends = Items(result, "friends");
            _requests = Items(result, "requests");
            _sent = Items(result, "sent");
        }
        catch (UserFacingException)
        {
        }
    }

    private static List<JsonNode> Items(JsonNode result, string key)
    {
        return result[key] is JsonArray array ? array.Where(item => item != null).Select(item => item!.DeepClone()).ToList() : [];
    }

    private async Task LoadPurchases()
    {
        if (!Own)
            return;

        try
        {
            _purchases = await ListAsync("/api/v1/purchases", "items");
            _subscriptions = await ListAsync("/api/v1/subscriptions", "items");
            _staffNotes = [];

            foreach (JsonNode dispute in await ListAsync("/api/v1/disputes", "items"))
            {
                var key = dispute.Str("payment_id") ?? dispute.Str("subscription_id");

                if (key != null && dispute.Str("staff_note") is { Length: > 0 } note)
                    _staffNotes[key] = note;
            }
        }
        catch (UserFacingException ex)
        {
            _purchaseError = ex.Message;
        }
    }

    private string Relation()
    {
        var id = _profile.Str("id");

        if (_friends.Any(friend => friend.Str("id") == id))
            return "friends";

        if (_requests.Any(friend => friend.Str("id") == id))
            return "incoming";

        return _sent.Any(friend => friend.Str("id") == id) ? "sent" : "none";
    }

    private void Act(string key, HttpMethod method, string path, object? body, string done)
    {
        if (!_busy.TryAdd(key, true))
            return;

        shell.Invalidate();

        _ = Task.Run(async () =>
        {
            try
            {
                await shell.App.Forge.SendJsonAsync(method, path, body);
                await LoadPeople();
                shell.Toast(done);
            }
            catch (UserFacingException ex)
            {
                shell.Toast(ex.Message);
            }

            _busy.TryRemove(key, out _);
            shell.Invalidate();
        });
    }

    private void AddFriend() => Act("friend", HttpMethod.Post, "/api/v1/friends", new { user_id = _profile.Str("id") }, "Friend request sent.");

    private void AcceptFriend(string id) => Act("accept-" + id, HttpMethod.Put, "/api/v1/friends/" + Uri.EscapeDataString(id), null, "Friend added.");

    private void RemoveFriend(string id, string done) => Act("remove-" + id, HttpMethod.Delete, "/api/v1/friends/" + Uri.EscapeDataString(id), null, done);

    private void StartChat()
    {
        if (!_busy.TryAdd("message", true))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode chat = await shell.App.Forge.SendJsonAsync(HttpMethod.Post, "/api/v1/chat", new { user_id = _profile.Str("id") });
                shell.OpenForge("/chat?c=" + Uri.EscapeDataString(chat.Str("id") ?? ""));
            }
            catch (UserFacingException ex)
            {
                shell.Toast(ex.Message);
            }

            _busy.TryRemove("message", out _);
            shell.Invalidate();
        });
    }

    private void Unsubscribe(string id)
    {
        if (!_busy.TryAdd("sub-" + id, true))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await shell.App.Forge.SendJsonAsync(HttpMethod.Post, $"/api/v1/subscriptions/{Uri.EscapeDataString(id)}/cancel", null);
                await LoadPurchases();
                shell.Toast("Unsubscribed. You keep access until the end of the month you paid for.");
            }
            catch (UserFacingException ex)
            {
                shell.Toast(ex.Message);
            }

            _busy.TryRemove("sub-" + id, out _);
            shell.Invalidate();
        });
    }
}
