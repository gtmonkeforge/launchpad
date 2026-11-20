using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ReleaseEditor
{
    private sealed record Asset(JsonNode? Id, string Name, long Size);

    private readonly List<Asset> _assets = [];
    private readonly List<bool> _picked = [];
    private bool _githubOpen;
    private string _githubUrl = "";
    private int _githubLoader;
    private string _githubStatus = "";
    private bool _githubBusy;
    private string _tag = "";
    private string _releaseNotes = "";

    private double DrawGitHub(Ui ui, double width, double y)
    {
        if (Widgets.Button(ui, "release-github-toggle", FormKit.Left, y + 4, _githubOpen ? "Hide GitHub import" : "Copy assets from a GitHub release", ButtonKind.Subtle, out _, true, 32))
            _githubOpen = !_githubOpen;

        y += 44;

        if (!_githubOpen)
            return y;

        ui.Pos(FormKit.Left, y);
        ui.Size(width - 290, FormKit.RowHeight);
        ui.InputEx("##release-github-url", ref _githubUrl, InputFlags.None, "https://github.com/owner/repo/releases/tag/v1.0.0", 500);
        ui.Pos(FormKit.Left + width - 280, y);
        ui.Size(150, FormKit.RowHeight);
        ui.Combo("##release-github-loader", ref _githubLoader, LoaderNames);

        if (Widgets.Button(ui, "release-github-load", FormKit.Left + width - 120, y + 2, _githubBusy ? "Loading..." : "Load release", ButtonKind.Subtle, out _, !_githubBusy && _githubUrl.Trim().Length > 0, 35))
            LoadRelease();

        y += 52;

        for (var i = 0; i < _assets.Count; i++)
        {
            var on = _picked[i];
            ui.Pos(FormKit.Left, y);

            if (ui.Checkbox(_assets[i].Name + "  " + UploadFiles.Size(_assets[i].Size), ref on))
                _picked[i] = on;

            y += 32;
        }

        if (_assets.Count > 0)
        {
            if (Widgets.Button(ui, "release-github-copy", FormKit.Left, y + 2, "Copy selected", ButtonKind.Primary, out _, !_githubBusy, 34))
                CopyAssets();

            y += 42;
        }

        if (_githubStatus.Length > 0)
            y = FormKit.Hint(ui, _githubStatus, y, width);

        return y;
    }

    private void LoadRelease()
    {
        _githubBusy = true;
        _githubStatus = "Loading release...";
        _assets.Clear();
        _picked.Clear();
        shell.Invalidate();

        _ = Task.Run(async () =>
        {
            try
            {
                var release = await shell.App.Forge.SendJsonAsync(HttpMethod.Post, "/api/v1/github-release", new { url = _githubUrl.Trim() });
                _tag = release.Str("tag") ?? "";
                _releaseNotes = release.Str("notes") ?? "";

                foreach (var asset in release["assets"] as JsonArray ?? [])
                {
                    _assets.Add(new Asset(asset?["id"]?.DeepClone(), asset.Str("name") ?? "asset", asset.Long("size")));
                    _picked.Add(false);
                }

                _githubStatus = _assets.Count == 0 ? "No DLL or ZIP assets of up to 50 MB were found." : "Choose one DLL, one ZIP, or one of each.";
            }
            catch (UserFacingException ex)
            {
                _githubStatus = ex.Message;
            }

            _githubBusy = false;
            shell.Invalidate();
        });
    }

    private void CopyAssets()
    {
        var chosen = _assets.Where((_, i) => _picked[i]).ToList();
        var kinds = chosen.Select(asset => Path.GetExtension(asset.Name).ToLowerInvariant()).ToList();

        if (chosen.Count == 0 || chosen.Count > 2 || kinds.Distinct().Count() != kinds.Count)
        {
            _githubStatus = "Choose one DLL, one ZIP, or one of each.";
            return;
        }

        _githubBusy = true;
        _githubStatus = "Copying assets...";
        var loader = _githubLoader;
        var url = _githubUrl.Trim();

        _ = Task.Run(async () =>
        {
            try
            {
                var folder = Directory.CreateTempSubdirectory("launchpad-github-").FullName;

                foreach (var asset in chosen)
                    await Fetch(url, asset, folder, loader);

                var version = Regex.Replace(_tag, "^v(?=\\d)", "", RegexOptions.IgnoreCase);

                if (Version.Length == 0 && VersionPattern().IsMatch(version))
                    Version = version;

                if (Notes.Length == 0)
                    Notes = _releaseNotes.Length > 10000 ? _releaseNotes[..10000] : _releaseNotes;

                _githubStatus = "Assets copied. Check the version and changelog, then publish.";
            }
            catch (Exception ex) when (ex is UserFacingException or IOException)
            {
                _githubStatus = ex.Message;
            }

            _githubBusy = false;
            shell.Invalidate();
        });
    }

    private async Task Fetch(string url, Asset asset, string folder, int loader)
    {
        var body = new StringContent(new JsonObject { ["url"] = url, ["asset_id"] = asset.Id?.DeepClone() }.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await shell.App.Forge.SendAsync(HttpMethod.Post, "/api/v1/github-release/asset", body);
        var path = Path.Combine(folder, Path.GetFileName(asset.Name));
        await File.WriteAllBytesAsync(path, await response.Content.ReadAsByteArrayAsync());

        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ZipPaths[loader] = path;
        else
            DllPaths[loader] = path;
    }
}
