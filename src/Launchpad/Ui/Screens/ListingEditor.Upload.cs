using System.Text.Json.Nodes;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

public sealed partial class ListingEditor
{
    public void Load(JsonNode mod)
    {
        Description = mod.Str("description") ?? "";
        ExistingLogo = mod.Str("logo");
        ExistingBanner = mod.Str("banner");
        ExistingShots = (mod["screenshots"] as JsonArray)?.Select(shot => shot?.GetValue<string>() ?? "").Where(shot => shot.Length > 0).ToList() ?? [];

        for (var i = 0; i < 3; i++)
            Capabilities[i] = mod["capabilities"].Bool(CapabilityIds[i]);

        var links = mod["links"] as JsonArray ?? [];

        for (var i = 0; i < 3; i++)
        {
            LinkLabels[i] = i < links.Count ? links[i].Str("label") ?? "" : "";
            LinkUrls[i] = i < links.Count ? links[i].Str("url") ?? "" : "";
        }
    }

    public string? Validate()
    {
        if (Description.Trim().Length == 0)
            return "Add a description before saving.";

        foreach (var path in new[] { Logo, Banner }.Concat(Screenshots))
        {
            var problem = UploadFiles.Check(path, 5 * UploadFiles.Megabyte, ImageTypes, "Each image must be 5 MB or smaller.", "Images must be PNG, JPG or WebP files.");

            if (problem != null)
                return problem;
        }

        return Screenshots.Count > MaxShots ? "Choose at most 20 screenshots." : null;
    }

    public JsonArray Links()
    {
        var links = new JsonArray();

        for (var i = 0; i < 3; i++)
        {
            var label = LinkLabels[i].Trim();
            var url = LinkUrls[i].Trim();

            if (label.Length > 0 || url.Length > 0)
                links.Add(new JsonObject { ["label"] = label, ["url"] = url });
        }

        return links;
    }

    public JsonObject Capability()
    {
        var caps = new JsonObject();

        for (var i = 0; i < 3; i++)
            caps[CapabilityIds[i]] = Capabilities[i];

        return caps;
    }

    public void AddFiles(MultipartFormDataContent form)
    {
        UploadFiles.Add(form, "logo", Logo);
        UploadFiles.Add(form, "banner", Banner);

        foreach (var path in Screenshots)
            UploadFiles.Add(form, "screenshots", path);
    }

    public void AddPublishFields(MultipartFormDataContent form)
    {
        form.Add(new StringContent(Description.Replace("\r", "")), "description");
        form.Add(new StringContent(Links().ToJsonString()), "links");

        for (var i = 0; i < 3; i++)
            form.Add(new StringContent(Capabilities[i] ? "true" : "false"), CapabilityIds[i]);

        form.Add(new StringContent("false"), "remove_logo");
        form.Add(new StringContent("false"), "remove_screenshots");
        AddFiles(form);
    }

    public JsonObject RemovalFlags() => new()
    {
        ["remove_logo"] = RemoveLogo && Logo.Trim().Length == 0,
        ["remove_banner"] = RemoveBanner && Banner.Trim().Length == 0,
        ["remove_screenshots"] = RemoveShots && Screenshots.Count == 0,
    };
}
