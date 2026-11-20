using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

public sealed partial class PublishScreen
{
    [GeneratedRegex(@"^[^\s/\\?#%]{1,200}$")]
    private static partial Regex GuidPattern();

    private string? Validate()
    {
        if (_name.Trim().Length == 0 || _listing.Description.Trim().Length == 0 || !GuidPattern().IsMatch(_guid.Trim()))
            return "Complete the mod name, a GUID (no spaces or / \\ ? # %) and a description before publishing.";

        return _listing.Validate() ?? _release.Validate();
    }

    private async Task Publish()
    {
        _error = Validate();

        if (_error != null)
            return;

        _busy = true;
        shell.Invalidate();

        try
        {
            using var form = BuildForm();
            using var response = await shell.App.Forge.SendAsync(HttpMethod.Post, "/api/v1/mods", form);
            var result = JsonNode.Parse(await response.Content.ReadAsStringAsync()) as JsonObject;
            var guid = result.Str("guid") ?? _guid.Trim();
            await SaveSlug(guid);
            _publishedGuid = guid;
        }
        catch (UserFacingException ex)
        {
            _error = ex.Message;
        }
        catch (JsonException)
        {
            _error = "MonkeForge sent a response Launchpad couldn't read.";
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _busy = false;
            shell.Invalidate();
        }
    }

    private async Task SaveSlug(string guid)
    {
        var slug = _slug.Trim();

        if (slug.Length == 0 || !_authors![_author].Verified)
            return;

        try
        {
            await shell.App.Forge.SendJsonAsync(HttpMethod.Patch, "/api/v1/mods/" + Uri.EscapeDataString(guid) + "/url", new { custom_slug = slug });
        }
        catch (UserFacingException ex)
        {
            shell.Toast(ex.Message);
        }
    }

    private MultipartFormDataContent BuildForm()
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(_name.Trim()), "name");
        form.Add(new StringContent(_guid.Trim()), "guid");
        form.Add(new StringContent(Categories[_category]), "category");
        form.Add(new StringContent(_source.Trim()), "source_url");
        form.Add(new StringContent(VisibilityValues[_visibility]), "visibility");
        form.Add(new StringContent(_injector ? "true" : "false"), "uses_injector");

        if (_authors!.Count > 1)
            form.Add(new StringContent(_authors[_author].Id), "author_id");

        _listing.AddPublishFields(form);
        _release.AddFields(form);
        return form;
    }
}
