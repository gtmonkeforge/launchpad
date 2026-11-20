using GoomeSharp;
using System.Text.Json.Nodes;
using Launchpad.Forge;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

public sealed partial class BrowseScreen
{
    private const int PageSize = 12;

    private static readonly string[] SortKeys = ["newest", "popular", "name"];
    private static readonly string[] CapabilityKeys = ["", "auto_update", "extensions", "internet"];
    private static readonly string[] LoaderKeys = ["", "bepinex5", "bepinex6", "melonloader"];
    private static readonly string[] FormatKeys = ["", "dll", "zip"];
    private static readonly string[] PriceKeys = ["", "free", "paid"];

    private List<JsonNode> _items = [];
    private int _total;
    private int _page = 1;
    private int _request;
    private bool _loading;
    private string? _error;

    private string _category = "";
    private int _capability;
    private int _price;
    private int _loader;
    private int _format;
    private bool _installed;
    private bool _mineOnly;

    private bool Filtered => shell.Search.Trim().Length > 0 || _category.Length > 0 || _capability > 0 || _price > 0 || _loader > 0 || _format > 0 || _mineOnly;

    private void Load(int page)
    {
        var request = ++_request;
        _page = page;
        _loading = true;
        _error = null;
        var query = shell.Search.Trim();
        shell.Invalidate();

        _ = Task.Run(async () =>
        {
            try
            {
                var (items, total) = await FetchAsync(query, page);

                if (request != _request)
                    return;

                _items = items;
                _total = total;
            }
            catch (UserFacingException ex)
            {
                if (request == _request)
                    _error = ex.Message;
            }

            if (request == _request)
                _loading = false;

            shell.Invalidate();
        });
    }

    private string Filters(string query)
    {
        var parts = new List<string>();
        Add(parts, "q", query);
        Add(parts, "category", _category);
        Add(parts, "capability", CapabilityKeys[_capability]);
        Add(parts, "loader", LoaderKeys[_loader]);
        Add(parts, "type", FormatKeys[_format]);
        Add(parts, "price", PriceKeys[_price]);

        if (_mineOnly)
            parts.Add("mine=true");

        return string.Join('&', parts);
    }

    private static void Add(List<string> parts, string key, string value)
    {
        if (value.Length > 0)
            parts.Add(key + "=" + Uri.EscapeDataString(value));
    }

    private async Task<(List<JsonNode> Items, int Total)> FetchAsync(string query, int page)
    {
        var filters = Filters(query);
        var sort = "sort=" + SortKeys[_sort];

        if (_installed)
            return await FetchInstalledAsync(filters + (filters.Length > 0 ? "&" : "") + sort, page);

        JsonNode result = await shell.App.Forge.GetJsonAsync($"/api/v1/mods?{sort}&page={page}&limit={PageSize}" + (filters.Length > 0 ? "&" + filters : ""));
        return (Items(result), (int)result.Long("total"));
    }

    private async Task<(List<JsonNode> Items, int Total)> FetchInstalledAsync(string filters, int page)
    {
        const int Batch = 100, MaxBatches = 20;
        var installed = new HashSet<string>((await shell.App.Mods.ListAsync()).Select(mod => mod.Guid), StringComparer.Ordinal);
        var matches = new List<JsonNode>();

        for (var batch = 1; installed.Count > 0 && batch <= MaxBatches; batch++)
        {
            JsonNode result = await shell.App.Forge.GetJsonAsync($"/api/v1/mods?{filters}&page={batch}&limit={Batch}");
            var items = Items(result);
            matches.AddRange(items.Where(item => installed.Contains(item.Str("guid") ?? "")));

            if (items.Count < Batch || batch * Batch >= result.Long("total"))
                break;
        }

        return (matches.Skip((page - 1) * PageSize).Take(PageSize).ToList(), matches.Count);
    }

    private static List<JsonNode> Items(JsonNode result)
    {
        return result["items"] is JsonArray array ? array.Where(item => item != null).Select(item => item!.DeepClone()).ToList() : [];
    }
}
