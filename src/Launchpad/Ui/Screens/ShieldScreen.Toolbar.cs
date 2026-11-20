using System.Text.Json;
using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;
using static Launchpad.Ui.Screens.ModScreen;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ShieldScreen
{
    private const int CheckBatch = 40;
    private const double CheckRow = 48;

    private bool _assemblies;
    private bool _onlyFailing;
    private string _filter = "";
    private int _checksShown = CheckBatch;
    private readonly HashSet<string> _openChecks = [];

    private sealed record Section(JsonNode Artifact, string Name, List<(string Key, JsonNode Check)> Checks);

    private static bool Failed(JsonNode? check) => check.Str("status") == "Fail";

    private static bool Passing(JsonNode? check) => check.Str("status") is "Pass" or "Inconclusive";

    private JsonArray Artifacts => _report?["artifacts"] as JsonArray ?? [];

    private int FailingCount => Artifacts.Sum(artifact => (artifact?["report"]?["checks"] as JsonArray)?.Count(Failed) ?? 0);

    private List<Section> Sections(out int hidden)
    {
        var sections = new List<Section>();
        var budget = _checksShown;
        hidden = 0;
        var text = _filter.Trim().ToLowerInvariant();

        for (var a = 0; a < Artifacts.Count; a++)
        {
            var kept = new List<(string, JsonNode)>();

            foreach (var (check, index) in ((Artifacts[a]?["report"]?["checks"] as JsonArray) ?? []).Select((check, index) => (check, index)))
            {
                var haystack = $"{check.Str("title")} {check.Str("ruleId")} {check.Str("category")}".ToLowerInvariant();

                if (!haystack.Contains(text) || (_onlyFailing && !Failed(check)))
                    continue;

                if (budget-- > 0)
                    kept.Add((a + ":" + index, check!));
                else
                    hidden++;
            }

            sections.Add(new Section(Artifacts[a]!, Artifacts[a].Str("name") ?? "", kept));
        }

        return sections;
    }

    private double DrawToolbar(Ui ui, double left, double width, double y)
    {
        Item bar = Area(ui, "shield-bar", left, y, width, 38);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, bar.X, bar.Y, 190, 38, Palette.Radius, Palette.Surface, Palette.Line);
        var views = new[] { "Checks", "Assemblies" };

        for (var i = 0; i < views.Length; i++)
        {
            Item tab = Chrome.Hit(ui, "shield-view-" + i, left + 4 + i * 91, y + 4, 91, 30);
            c = ui.Canvas()!;
            var active = _assemblies == (i == 1);

            if (active)
                c.DrawRRect(tab.X, tab.Y, 91, 30, 7, Palette.Surface2);

            Chrome.Text(c, Fonts.Main, 13.5, tab.X + 45 - Fonts.Main.Measure(13.5, views[i]) / 2, tab.Y + 15, views[i], active || tab.Hovered ? Palette.Text : Palette.Muted);

            if (tab.Clicked)
                _assemblies = i == 1;
        }

        var copyWidth = Widgets.ButtonWidth("Copy JSON");

        if (Widgets.Button(ui, "shield-copy", left + width - copyWidth, y + 2, "Copy JSON", ButtonKind.Subtle, out _))
        {
            _shell.Window.Goome.Clipboard = _report!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            _shell.Toast("Report copied.");
        }

        if (_assemblies)
            return y + 38;

        ui.Pos(left + 206, y + 8);

        if (ui.Checkbox($"Only failing ({FailingCount})", ref _onlyFailing))
            _checksShown = CheckBatch;

        ui.Pos(left + width - copyWidth - 12 - 220, y + 2);
        ui.Width(220);

        var before = _filter;
        ui.InputEx("##shield-check-filter", ref _filter, InputFlags.None, "Filter checks…", 80);

        if (before != _filter)
            _checksShown = CheckBatch;

        return y + 38;
    }
}
