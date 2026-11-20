using System.Text.Json.Nodes;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

public sealed partial class DocsScreen
{
    private static JsonNode? _cachedIndex;

    private JsonNode? _index = _cachedIndex;
    private JsonNode? _page;
    private string? _error;
    private bool _started;

    private void Load()
    {
        if (_started)
            return;

        _started = true;

        _ = Task.Run(async () =>
        {
            try
            {
                if (_index == null)
                {
                    _index = await _shell.App.Forge.GetJsonAsync("/api/v1/docs");
                    _cachedIndex = _index;
                }

                _page = await _shell.App.Forge.GetJsonAsync("/api/v1/docs/pages/" + _slug);
            }
            catch (UserFacingException ex)
            {
                _error = ex.Message;
            }

            _shell.Invalidate();
        });
    }

    private List<JsonNode> Pages()
    {
        return (_index?["pages"] as JsonArray)?.Where(page => page != null).Select(page => page!).ToList() ?? [];
    }

    private List<(string Section, List<JsonNode> Items)> Groups()
    {
        var groups = new List<(string Section, List<JsonNode> Items)>();

        foreach (var page in Pages().Where(page => page.Str("slug") != Home))
        {
            var section = page.Str("section") ?? "";
            var group = groups.FindIndex(existing => existing.Section == section);

            if (group < 0)
                groups.Add((section, [page]));
            else
                groups[group].Items.Add(page);
        }

        return groups.OrderBy(group => group.Section.Length > 0).ToList();
    }

    private List<JsonNode> Order()
    {
        var order = Pages().Where(page => page.Str("slug") == Home).ToList();
        order.AddRange(Groups().SelectMany(group => group.Items));
        return order;
    }
}
