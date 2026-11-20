using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
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

    private double DrawSecurity(Ui ui, double left, double width, double y)
    {
        if (_report == null)
            return DrawSecurityWaiting(ui, left, width, y);

        y = DrawSecuritySummary(ui, left, width, y) + 14;
        y = DrawSecurityToolbar(ui, left, width, y) + 14;
        y = _assemblies ? DrawAssemblies(ui, left, width, y) : DrawChecks(ui, left, width, y);
        Item hint = Area(ui, "mod-security-hint", left, y + 4, width, 24);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 13, hint.X, hint.Y + 12, "Static analysis is not a guarantee of safety. Reports reflect the latest release in this channel.", Palette.Faint);
        return y + 28;
    }

    private double DrawSecurityWaiting(Ui ui, double left, double width, double y)
    {
        Item box = Area(ui, "mod-security-wait", left, y, width, 110);
        Chrome.Box(ui.Canvas()!, box.X, box.Y, width, 110, Palette.RadiusLarge, Palette.Surface, Palette.Line);

        if (_reportFailed)
            Chrome.Text(ui.Canvas()!, Fonts.Main, 14, box.X + 22, box.Y + 55, "Analysis unavailable.", Palette.Muted);
        else
        {
            ui.Pos(left + width / 2 - 10, y + 45);
            ui.Spinner(20);
        }

        return y + 110;
    }

    private double DrawSecuritySummary(Ui ui, double left, double width, double y)
    {
        var complete = _report.Str("status") == "complete";
        var total = _report.Long("total_checks");
        var failed = _report.Long("failed_checks");
        var measured = complete && total > 0;
        var risk = measured ? failed * 100.0 / total : 0;
        var eligible = _report.Bool("eligible");
        var verdict = complete ? (eligible ? "Passed" : "Review required") : StatusLabel(_report.Str("status"));
        var heading = complete ? (eligible ? "Meets the automatic approval policy" : "This release needs a manual review") : "Analysis is incomplete or still running";
        var textLeft = 190.0;
        var textWidth = width - textLeft - 24;
        var diagnostics = (_report?["diagnostics"] as JsonArray)?.Select(item => item?.GetValue<string>() ?? "").ToList() ?? [];
        var diagLines = diagnostics.Select(text => Chrome.Wrap(Fonts.Main, 13.5, text, textWidth - 28, 3)).ToList();
        var hint = Chrome.Wrap(Fonts.Main, 13.5, "Inconclusive checks count as passing. Unevaluated checks require review." + (complete ? "" : " Refresh to check for results."), textWidth, 3);
        var height = Math.Max(220, 24 + 52 + 3 * 28 + 6 + diagLines.Sum(lines => lines.Count * 21 + 22) + hint.Count * 21 + 24);

        Item card = Area(ui, "mod-security", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        DrawRing(c, card.X + 90, card.Y + 92, measured, risk, eligible);
        var pillWidth = Fonts.Main.Measure(12.5, verdict) + 18;
        Widgets.Pill(c, card.X + 90 - pillWidth / 2, card.Y + 166, verdict, eligible && measured ? Palette.Good : measured ? Palette.Bad : Palette.Muted);

        var x = card.X + textLeft;
        var top = card.Y + 24;
        Chrome.BoldText(c, Fonts.Main, 17, x, top + 10, heading, Palette.Text);
        var scanned = _report.Str("scanned_at");
        var sub = (_reportVersion != null ? "v" + _reportVersion + " · " : "") + _channel + (scanned != null ? " · scanned " + DateText(scanned) : "");
        Chrome.Text(c, Fonts.Mono, 12.5, x, top + 34, sub, Palette.Faint);
        top += 52;

        var threshold = _report?["threshold_percent"]?.GetValue<double>() ?? 2.5;
        var rows = new (string Label, string Value)[]
        {
            ("Checks", $"{total} total · {failed} failed"),
            ("Assemblies", Artifacts.Count.ToString(CultureInfo.InvariantCulture)),
            ("Threshold", $"< {threshold.ToString("0.##", CultureInfo.InvariantCulture)}% failed per assembly"),
        };

        foreach (var (label, value) in rows)
        {
            Chrome.Text(c, Fonts.Main, 13.5, x, top + 12, label, Palette.Muted);
            Chrome.Text(c, Fonts.Mono, 13, x + 110, top + 12, value, Palette.Text);
            top += 28;
        }

        top += 6;

        for (var i = 0; i < diagnostics.Count; i++)
        {
            var boxHeight = diagLines[i].Count * 21 + 14;
            Chrome.Box(c, x, top, textWidth, boxHeight, Palette.Radius, 0x14E5B85C, 0x5CE5B85C);

            for (var line = 0; line < diagLines[i].Count; line++)
                Chrome.Text(c, Fonts.Main, 13.5, x + 14, top + 17 + line * 21, diagLines[i][line], Palette.Warn);

            top += boxHeight + 8;
        }

        for (var i = 0; i < hint.Count; i++)
            Chrome.Text(c, Fonts.Main, 13.5, x, top + 14 + i * 21, hint[i], Palette.Faint);

        return y + height;
    }

    private double DrawSecurityToolbar(Ui ui, double left, double width, double y)
    {
        Item bar = Area(ui, "mod-security-bar", left, y, width, 38);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, bar.X, bar.Y, 190, 38, Palette.Radius, Palette.Surface, Palette.Line);
        var views = new[] { "Checks", "Assemblies" };

        for (var i = 0; i < views.Length; i++)
        {
            Item tab = Chrome.Hit(ui, "mod-security-view-" + i, left + 4 + i * 91, y + 4, 91, 30);
            c = ui.Canvas()!;
            var active = _assemblies == (i == 1);

            if (active)
                c.DrawRRect(tab.X, tab.Y, 91, 30, 7, Palette.Surface2);

            Chrome.Text(c, Fonts.Main, 13.5, tab.X + 45 - Fonts.Main.Measure(13.5, views[i]) / 2, tab.Y + 15, views[i], active || tab.Hovered ? Palette.Text : Palette.Muted);

            if (tab.Clicked)
                _assemblies = i == 1;
        }

        var failing = FailingCount;
        var copyWidth = Widgets.ButtonWidth("Copy JSON");

        if (Widgets.Button(ui, "mod-security-copy", left + width - copyWidth, y + 2, "Copy JSON", ButtonKind.Subtle, out _))
        {
            _shell.Window.Goome.Clipboard = _report!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            _shell.Toast("Report copied.");
        }

        if (_assemblies)
            return y + 38;

        ui.Pos(left + 206, y + 8);

        if (ui.Checkbox($"Only failing ({failing})", ref _onlyFailing))
            _checksShown = CheckBatch;

        ui.Pos(left + width - copyWidth - 12 - 220, y + 2);
        ui.Width(220);

        var before = _filter;
        ui.InputEx("##mod-check-filter", ref _filter, InputFlags.None, "Filter checks…", 80);

        if (before != _filter)
            _checksShown = CheckBatch;

        return y + 38;
    }
}
