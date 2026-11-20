using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

public sealed partial class PublishScreen
{
    private const long Megabyte = 1024 * 1024;

    [GeneratedRegex(@"^[^\s/\\?#%]{1,200}$")]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"^[0-9][a-zA-Z0-9.+_-]{0,63}$")]
    private static partial Regex VersionPattern();

    private string? Validate()
    {
        if (_name.Trim().Length == 0 || _description.Trim().Length == 0 || !GuidPattern().IsMatch(_guid.Trim()))
            return "Complete the mod name, a GUID (no spaces or / \\ ? # %) and a description before publishing.";

        foreach (var path in new[] { _logoPath, _bannerPath })
        {
            var problem = CheckFile(path, 5 * Megabyte, [".png", ".jpg", ".jpeg", ".webp"], "Each image must be 5 MB or smaller.");

            if (problem != null)
                return problem;
        }

        if (!VersionPattern().IsMatch(_version.Trim()))
            return "Use a version such as 1.0.0.";

        if (Enumerable.Range(0, 3).All(i => _dllPaths[i].Trim().Length == 0 && _zipPaths[i].Trim().Length == 0))
            return "Attach downloads for at least one mod loader.";

        for (var i = 0; i < 3; i++)
        {
            var problem = CheckFile(_dllPaths[i], 50 * Megabyte, [".dll"], "Each download must be 50 MB or smaller.")
                ?? CheckFile(_zipPaths[i], 50 * Megabyte, [".zip"], "Each download must be 50 MB or smaller.");

            if (problem != null)
                return problem;
        }

        return null;
    }

    private static string? CheckFile(string raw, long maxBytes, string[] extensions, string tooBig)
    {
        var path = raw.Trim();

        if (path.Length == 0)
            return null;

        if (!File.Exists(path))
            return "Couldn't find the file " + path;

        if (!extensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            return "Each loader supports one DLL, one ZIP, or one of each.";

        return new FileInfo(path).Length > maxBytes ? tooBig : null;
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
        form.Add(new StringContent(_description.Replace("\\n", "\n")), "description");
        form.Add(new StringContent(Categories[_category]), "category");
        form.Add(new StringContent(_source.Trim()), "source_url");
        form.Add(new StringContent(_version.Trim()), "version");
        form.Add(new StringContent("stable"), "channel");
        form.Add(new StringContent(_notes), "notes");
        form.Add(new StringContent(LinksJson()), "links");
        form.Add(new StringContent(VisibilityValues[_visibility]), "visibility");
        form.Add(new StringContent(_injector ? "true" : "false"), "uses_injector");

        if (_authors!.Count > 1)
            form.Add(new StringContent(_authors[_author].Id), "author_id");

        for (var i = 0; i < 3; i++)
            form.Add(new StringContent(_capabilities[i] ? "true" : "false"), CapabilityIds[i]);

        foreach (var key in new[] { "logo", "screenshots" })
            form.Add(new StringContent("false"), "remove_" + key);

        AddFile(form, "logo", _logoPath);
        AddFile(form, "banner", _bannerPath);

        for (var i = 0; i < 3; i++)
        {
            AddFile(form, "downloads_" + LoaderIds[i], _dllPaths[i]);
            AddFile(form, "downloads_" + LoaderIds[i], _zipPaths[i]);
        }

        return form;
    }

    private string LinksJson()
    {
        var links = Enumerable.Range(0, 3)
            .Select(i => new { label = _linkLabels[i].Trim(), url = _linkUrls[i].Trim() })
            .Where(link => link.label.Length > 0 || link.url.Length > 0);

        return JsonSerializer.Serialize(links);
    }

    private static void AddFile(MultipartFormDataContent form, string field, string raw)
    {
        var path = raw.Trim();

        if (path.Length == 0)
            return;

        var content = new ByteArrayContent(File.ReadAllBytes(path));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, field, Path.GetFileName(path));
    }
}
