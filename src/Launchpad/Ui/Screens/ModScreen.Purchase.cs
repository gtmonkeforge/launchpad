using System.Text.Json.Nodes;
using Launchpad.Infrastructure;
using Launchpad.Mods;

namespace Launchpad.Ui.Screens;

public sealed partial class ModScreen
{
    private sealed record PurchaseOption(string Value, string Label, string Button, bool Monthly);

    private int _choice;
    private bool _buying;
    private bool _polling;

    private JsonArray Tiers => _mod?["tiers"] as JsonArray ?? [];

    private string Currency => _mod.Str("currency") ?? "usd";

    private List<PurchaseOption> PurchaseOptions()
    {
        var options = new List<PurchaseOption>();

        if (_mod.Long("price_cents") > 0)
        {
            var price = Format.Money(_mod.Long("price_cents"), Currency);
            options.Add(new PurchaseOption("once", "One-time purchase · " + price, "Buy · " + price, false));
        }

        foreach (var tier in Tiers.Where(tier => !tier.Bool("subscribed")))
        {
            var price = Format.Money(tier.Long("price_cents"), tier.Str("currency") ?? Currency) + "/month";
            options.Add(new PurchaseOption("tier:" + tier.Str("id"), tier.Str("name") + " · " + price, "Subscribe · " + price, true));
        }

        return options;
    }

    private (string Label, bool Enabled, Action? Click) LockedPrimary()
    {
        var options = PurchaseOptions();

        if (!_shell.SignedIn)
            return ("Sign in to buy", true, () => _shell.Router.Push(new Route(Page.SignIn)));

        if (options.Count == 0)
            return ("Not for sale", false, null);

        if (_buying)
            return ("Opening checkout…", false, null);

        if (options.Count == 1)
            return (options[0].Button, true, () => StartPurchase(options[0]));

        return ("See options", true, () => ShowTab("Tiers"));
    }

    private void StartPurchase(PurchaseOption option)
    {
        var path = option.Value == "once" ? BasePath + "/checkout" : "/api/v1/tiers/" + Uri.EscapeDataString(option.Value["tier:".Length..]) + "/subscribe";
        _buying = true;
        _result = null;
        _failure = null;
        _shell.Toast("Taking you to checkout…");

        _ = Task.Run(async () =>
        {
            try
            {
                var reply = await _shell.App.Forge.SendJsonAsync(HttpMethod.Post, path, null);
                _shell.Call("shell.open", new JsonObject { ["url"] = reply.Str("url") });
                _result = "Finish paying in your browser. This page updates when the payment goes through.";
                WaitForPurchase();
            }
            catch (UserFacingException ex)
            {
                _failure = ex.Message;
            }

            _buying = false;
            _shell.Invalidate();
        });
    }

    private void WaitForPurchase()
    {
        if (_polling)
            return;

        _polling = true;
        var request = _request;

        _ = Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 90 && request == _request && _mod.Bool("gated") && !_mod.Bool("owned"); attempt++)
            {
                await Task.Delay(4000);

                try
                {
                    JsonNode latest = await _shell.App.Forge.GetJsonAsync(ModManager.ModPath(_guid, _channel));

                    if (latest.Bool("owned") && request == _request)
                    {
                        _shell.Toast("Your purchase is ready.");
                        Load();
                        break;
                    }
                }
                catch (UserFacingException)
                {
                }
            }

            _polling = false;
        });
    }
}
