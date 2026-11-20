using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ManageScreen
{
    private List<JsonNode> _tiers = [];
    private bool _tierForm;
    private string? _tierId;
    private string _tierName = "";
    private string _tierPrice = "";
    private string _tierBenefits = "";

    private void FillTiers(JsonNode mod)
    {
        _tiers = (mod["tiers"] as JsonArray ?? []).Where(tier => tier.Str("scope") == "mod").Select(tier => tier!.DeepClone()).ToList();
    }

    private double DrawTiers(Ui ui, double width, double y)
    {
        if (_mod.Str("visibility") != "public")
            return y;

        FormKit.Heading(ui, "manage-tiers-head", "Monthly tiers", y);
        y = FormKit.Hint(ui, "Subscribers to any of these tiers can install this mod. MonkeForge keeps " + _store.Long("fee_percent") + "% of each payment.", y + 34, width);

        if (_tiers.Count == 0)
            y = FormKit.Hint(ui, "No tiers yet.", y, width);

        for (var i = 0; i < _tiers.Count; i++)
            y = DrawTierRow(ui, _tiers[i], i, y, width);

        if (!_tierForm && CanSell && FormKit.ActionButton(ui, "manage-tier-add", "Add a tier", FormKit.Left, y))
            OpenTier(null);

        if (_tierForm)
            y = DrawTierForm(ui, width, y);

        return y + 50;
    }

    private double DrawTierRow(Ui ui, JsonNode tier, int index, double y, double width)
    {
        Item row = Chrome.Hit(ui, "manage-tier-row-" + index, FormKit.Left, y, width, 52);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, row.X, row.Y, width, 52, Palette.Radius, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 14.5, row.X + 16, row.Y + 18, Chrome.Clip(Fonts.Main, 14.5, tier.Str("name") ?? "", width - 300), Palette.Text);
        Chrome.Text(c, Fonts.Main, 13, row.X + 16, row.Y + 37, Format.Money(tier.Long("price_cents"), tier.Str("currency") ?? Currency) + "/month", Palette.Muted);
        var retire = Widgets.ButtonWidth("Retire");

        if (Widgets.Button(ui, "manage-tier-retire-" + index, FormKit.Left + width - 12 - retire, y + 9, "Retire", ButtonKind.Danger, out _, !_busy, 34))
            AskRetire(tier.Str("id") ?? "");

        if (Widgets.Button(ui, "manage-tier-edit-" + index, FormKit.Left + width - 20 - retire - Widgets.ButtonWidth("Edit"), y + 9, "Edit", ButtonKind.Subtle, out _, !_busy, 34))
            OpenTier(tier);

        return y + 60;
    }

    private void OpenTier(JsonNode? tier)
    {
        _tierForm = true;
        _tierId = tier.Str("id");
        _tierName = tier.Str("name") ?? "";
        _tierPrice = tier == null ? "" : FromMinor(tier.Long("price_cents"), Currency);
        _tierBenefits = tier?["benefits"] is JsonArray benefits ? string.Join("\n", benefits.Select(benefit => benefit?.ToString())) : "";
    }

    private double DrawTierForm(Ui ui, double width, double y)
    {
        y = FormKit.Field(ui, "tier-name", "Tier name", ref _tierName, y, width, "Supporter", null, 60);
        y = FormKit.Field(ui, "tier-price", "Monthly price (" + Currency.ToUpperInvariant() + ")", ref _tierPrice, y, width / 2, "4.99", "Price changes only apply to new subscribers.", 20);
        y = FormKit.TextArea(ui, "tier-benefits", "Benefits (one per line, up to 10)", ref _tierBenefits, y, width, 90, null, 2000);

        if (FormKit.ActionButton(ui, "manage-tier-save", "Save tier", FormKit.Left, y, true, out var saveWidth, !_busy))
            SaveTier();

        if (FormKit.ActionButton(ui, "manage-tier-cancel", "Cancel", FormKit.Left + saveWidth + 10, y))
            _tierForm = false;

        return y + 44;
    }

    private void SaveTier()
    {
        var price = ToMinor(_tierPrice, Currency);

        if (price == null || _tierName.Trim().Length == 0)
        {
            _error = "Name the tier and enter a price such as 4.99.";
            return;
        }

        var payload = new JsonObject
        {
            ["name"] = _tierName.Trim(),
            ["price_cents"] = price,
            ["benefits"] = new JsonArray(_tierBenefits.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).Select(line => (JsonNode)JsonValue.Create(line)!).ToArray()),
        };

        var id = _tierId;

        if (id == null)
            payload["guid"] = _guid;

        Run("Tier saved.", async () =>
        {
            await _shell.App.Forge.SendJsonAsync(id == null ? HttpMethod.Post : HttpMethod.Patch, id == null ? "/api/v1/tiers" : "/api/v1/tiers/" + Uri.EscapeDataString(id), payload);
            _tierForm = false;
            await Refresh();
        });
    }

    private void AskRetire(string id)
    {
        _confirm.Ask("Retire this tier", "Retire this tier? Nobody new can join, and current subscribers stop being billed at the end of the month they already paid for.", "Retire", () => Run("Tier retired.", async () =>
        {
            await _shell.App.Forge.SendJsonAsync(HttpMethod.Delete, "/api/v1/tiers/" + Uri.EscapeDataString(id), null);
            await Refresh();
        }));
    }
}
