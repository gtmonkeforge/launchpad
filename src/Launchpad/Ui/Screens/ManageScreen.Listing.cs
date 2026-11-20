using GoomeSharp;
using System.Text.Json.Nodes;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ManageScreen
{
    private string _name = "";
    private string _source = "";
    private int _category;

    private void FillListing(JsonNode mod)
    {
        _name = mod.Str("name") ?? "";
        _source = mod.Str("source_url") ?? "";
        _category = Math.Max(0, Array.IndexOf(Categories, mod.Str("category")));
    }

    private double DrawListing(Ui ui, double width, double y)
    {
        y = FormKit.Field(ui, "manage-name", "Mod name", ref _name, y, width, "Your mod's display name", null, 400);
        y = FormKit.Label(ui, "GUID (permanent)", y);
        Item guid = Chrome.Hit(ui, "manage-guid", FormKit.Left, y, width, 24);
        Chrome.Text(ui.Canvas()!, Fonts.Mono, 14, guid.X, guid.Y + 12, _guid, Palette.Muted);
        y += 38;
        y = FormKit.Pair(ui, "manage-category", "Category", ref _category, Categories, y, width / 2 - 6);
        y = FormKit.Field(ui, "manage-source", "Source code (optional)", ref _source, y, width, "https://github.com/you/your-mod", null, 2000);
        y = _listing.Draw(ui, width, y);
        y = FormKit.Error(ui, _error, y, width);

        if (FormKit.ActionButton(ui, "manage-save-page", _busy ? "Saving..." : "Save page", FormKit.Left, y, true, out _, !_busy))
            SavePage();

        return y + 44;
    }

    private void SavePage()
    {
        _error = _name.Trim().Length == 0 ? "Give the mod a name." : _listing.Validate();

        if (_error != null)
            return;

        var metadata = new JsonObject
        {
            ["name"] = _name.Trim(),
            ["description"] = _listing.Description,
            ["category"] = Categories[_category],
            ["source_url"] = _source.Trim().Length == 0 ? null : _source.Trim(),
            ["links"] = _listing.Links(),
            ["capabilities"] = _listing.Capability(),
        };

        foreach (var flag in _listing.RemovalFlags())
            metadata[flag.Key] = flag.Value?.DeepClone();

        var wasApproved = _mod.Str("status") == "approved";

        Run("Page saved.", async () =>
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(metadata.ToJsonString()), "metadata");
            _listing.AddFiles(form);
            using var response = await _shell.App.Forge.SendAsync(HttpMethod.Patch, ModUrl(), form);
            var result = JsonNode.Parse(await response.Content.ReadAsStringAsync());

            if (wasApproved && result.Str("status") == "pending")
                _shell.Toast("Page saved. Changes are waiting for review.");

            _listing.Logo = "";
            _listing.Banner = "";
            _listing.Screenshots.Clear();
            await Refresh();
        });
    }
}
