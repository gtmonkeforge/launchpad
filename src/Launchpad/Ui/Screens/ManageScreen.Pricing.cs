using System.Globalization;
using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ManageScreen
{
    private static readonly string[] ZeroDecimal = ["bif", "clp", "djf", "gnf", "jpy", "kmf", "krw", "mga", "pyg", "rwf", "ugx", "vnd", "vuv", "xaf", "xof", "xpf"];

    private string _price = "0.00";
    private bool _makeInjector;

    private string Currency => _store.Str("currency") ?? "usd";

    private bool CanSell => _payouts.Bool("charges_enabled");

    private static string FromMinor(long minor, string currency) => ZeroDecimal.Contains(currency) ? minor.ToString(CultureInfo.InvariantCulture) : (minor / 100.0).ToString("0.00", CultureInfo.InvariantCulture);

    private static long? ToMinor(string text, string currency)
    {
        var digits = new string(text.Where(letter => char.IsDigit(letter) || letter == '.').ToArray());

        if (!double.TryParse(digits.Length == 0 ? "0" : digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return null;

        return (long)Math.Round(ZeroDecimal.Contains(currency) ? value : value * 100);
    }

    private void FillPricing(JsonNode mod)
    {
        _price = FromMinor(mod.Long("price_cents"), Currency);
        _makeInjector = false;
        FillTiers(mod);
    }

    private double DrawPricing(Ui ui, double width, double y)
    {
        FormKit.Heading(ui, "manage-pricing-head", "Pricing", y);
        y += 36;

        if (!_store.Bool("enabled"))
            return FormKit.Hint(ui, "Payments aren't set up on this server yet.", y, width);

        var currency = Currency.ToUpperInvariant();
        y = FormKit.Field(ui, "manage-price", "Price (" + currency + ")", ref _price, y, width / 2, "0.00", "Leave at 0 to keep this mod free. MonkeForge keeps " + _store.Long("fee_percent") + "% of each sale. Stripe's processing fee is taken from your share.", 20);

        if (!CanSell)
            y = DrawPayoutsNotice(ui, width, y);

        y = _mod.Bool("uses_injector") ? FormKit.Hint(ui, "This mod is installed through the injector.", y, width) : DrawInjectorOption(ui, width, y);
        y = FormKit.Error(ui, _error, y, width);

        if (FormKit.ActionButton(ui, "manage-pricing-save", "Save pricing", FormKit.Left, y, false, out _, !_busy))
            SavePricing();

        return DrawTiers(ui, width, y + 62);
    }

    private double DrawPayoutsNotice(Ui ui, double width, double y)
    {
        var text = "Set up payouts before you charge for this mod.";
        var height = Widgets.NoticeHeight(width, text);
        Item box = Chrome.Hit(ui, "manage-payout-notice", FormKit.Left, y, width, height);
        Widgets.Notice(ui.Canvas()!, box.X, box.Y, width, text);

        if (Widgets.Button(ui, "manage-payout-open", FormKit.Left, y + height + 8, "Set up payouts", ButtonKind.Subtle, out _, true, 34))
            _shell.OpenForge("/settings/payouts" + (PersonalMod ? "" : "?account=" + Uri.EscapeDataString(_mod!["author"].Str("id") ?? "")));

        return y + height + 58;
    }

    private double DrawInjectorOption(Ui ui, double width, double y)
    {
        ui.Pos(FormKit.Left, y);
        ui.Checkbox("Install through the injector", ref _makeInjector);
        return FormKit.Hint(ui, "For closed-source mods. Files are encrypted and can only be installed through Launchpad, never downloaded. Each loader takes one DLL. Injector mods are always reviewed by a person. This can't be undone.", y + 36, width);
    }

    private void SavePricing()
    {
        var price = ToMinor(_price, Currency);

        if (price == null)
        {
            _error = "Enter a price such as 4.99.";
            return;
        }

        var payload = new JsonObject();

        if (price != _mod.Long("price_cents"))
            payload["price_cents"] = price;

        if (_makeInjector)
            payload["uses_injector"] = true;

        if (payload.Count == 0)
        {
            _shell.Toast("Pricing saved.");
            return;
        }

        if (_makeInjector)
        {
            _confirm.Ask("Switch to injector installs", "Switch this mod to injector installs? Existing files will be encrypted and stop being downloadable. This can't be undone.", "Switch", () => SendPricing(payload));
            return;
        }

        SendPricing(payload);
    }

    private void SendPricing(JsonObject payload)
    {
        Run("Pricing saved.", async () =>
        {
            var result = await _shell.App.Forge.SendJsonAsync(HttpMethod.Put, ModUrl("/pricing"), payload);

            if (payload.ContainsKey("uses_injector") && result.Str("status") == "pending")
                _shell.Toast("Injector installs enabled. The mod will be reviewed again before it's public.");

            await Refresh();
        });
    }
}
