using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui;

public sealed class Notifications(Shell shell)
{
    private const long RefreshMs = 30000;

    private long _lastFetch;
    private bool _fetching;

    public List<JsonNode> Items { get; private set; } = [];

    public void Watch()
    {
        if (!shell.SignedIn)
        {
            Items = [];
            return;
        }

        if (_fetching || (_lastFetch != 0 && Goome.NowMs - _lastFetch < RefreshMs))
            return;

        Refresh();
    }

    public void Refresh()
    {
        _fetching = true;
        _lastFetch = Goome.NowMs;

        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode result = await shell.App.Forge.GetJsonAsync("/api/v1/notifications");
                Items = result["items"] is JsonArray array ? array.Where(item => item != null).Select(item => item!.DeepClone()).Reverse().ToList() : [];
            }
            catch (UserFacingException)
            {
            }

            _fetching = false;
            shell.Invalidate();
        });
    }

    public void MarkAllRead()
    {
        var newest = Items.Max(item => item.Long("id"));
        Items = [];

        _ = Task.Run(async () =>
        {
            try
            {
                await shell.App.Forge.SendJsonAsync(HttpMethod.Post, "/api/v1/notifications/read", new { through = newest });
            }
            catch (UserFacingException ex)
            {
                shell.Toast(ex.Message);
            }

            Refresh();
        });
    }

    public static string Link(JsonNode item)
    {
        var target = Uri.EscapeDataString(item.Str("target") ?? "");

        return item.Str("kind") switch
        {
            "friend_request" or "friend_accepted" => "/friends",
            "message_request" or "chat_message" => "/chat?c=" + target,
            "group_member" => "/groups",
            "mod_transfer" => "/transfers",
            "purchase" or "mod_review" => "/view/" + target,
            "subscription" or "dispute" => "/profile#purchases",
            "dispute_staff" => "/admin",
            "sale_refund" => "/settings/payouts",
            "payout_privacy" => "/settings/payouts?account=" + target,
            _ => "",
        };
    }
}
