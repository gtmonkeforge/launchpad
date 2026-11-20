using System.Globalization;
using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;
using static Launchpad.Ui.Screens.ModScreen;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ShieldScreen : IScreen
{
    private const double SideMargin = 30;
    private const double RightGutter = 57;

    private readonly Shell _shell;
    private readonly string _guid;
    private readonly string _channel;
    private JsonNode? _report;
    private string? _reportVersion;
    private string? _name;
    private bool _failed;
    private int _request;

    public ShieldScreen(Shell shell, string? arg)
    {
        _shell = shell;
        var parts = (arg ?? "").Split('?', 2);
        _guid = parts[0];
        _channel = "stable";

        foreach (var pair in parts.Length > 1 ? parts[1].Split('&') : [])
        {
            if (pair.StartsWith("channel=", StringComparison.Ordinal))
                _channel = Uri.UnescapeDataString(pair["channel=".Length..]);
        }

        Load();
    }

    public void Reload() => Load();

    public void Leave() => _request++;

    public void Dispose() => _request++;

    private void Load()
    {
        _report = null;
        _failed = false;
        var request = ++_request;
        var basePath = "/api/v1/mods/" + Uri.EscapeDataString(_guid);
        var query = "?channel=" + Uri.EscapeDataString(_channel);

        _ = Task.Run(async () =>
        {
            try
            {
                var name = _shell.App.Forge.GetJsonAsync(basePath + query);
                JsonNode result = await _shell.App.Forge.GetJsonAsync(basePath + "/monkeshield-report" + query);

                if (request == _request)
                {
                    _report = result["report"];
                    _reportVersion = result.Str("version");
                }

                try
                {
                    _name = (await name).Str("name");
                }
                catch (UserFacingException)
                {
                }
            }
            catch (UserFacingException)
            {
                if (request == _request)
                    _failed = true;
            }

            _shell.Invalidate();
        });
    }

    public void Draw(Ui ui, double width, double height)
    {
        ui.ScrollBegin("shield-" + _guid, height);
        ui.Pos(0, 0);
        ui.Custom("shield-origin", 1, 1, ItemFlags.NoHit, out Item _);

        var contentWidth = Math.Max(320, width - SideMargin - RightGutter);
        var y = DrawBackLink(ui, 22) + 14;

        if (_report == null)
            y = DrawWaiting(ui, contentWidth, y);
        else
        {
            y = DrawHead(ui, contentWidth, y) + 20;
            y = DrawSummary(ui, SideMargin, contentWidth, y) + 14;
            y = DrawToolbar(ui, SideMargin, contentWidth, y) + 14;
            y = _assemblies ? DrawAssemblies(ui, SideMargin, contentWidth, y) : DrawChecks(ui, SideMargin, contentWidth, y);
            Item hint = Area(ui, "shield-hint", SideMargin, y + 4, contentWidth, 24);
            Chrome.Text(ui.Canvas()!, Fonts.Main, 13, hint.X, hint.Y + 12, "Static analysis is not a guarantee of safety. Reports reflect the latest release in this channel.", Palette.Faint);
            y += 28;
        }

        _shell.Footer.Finish(ui, "shield-end", y, height, width);
        ui.ScrollEnd();
    }

    private void OpenMod()
    {
        _shell.Router.Push(new Route(Page.Mod, _guid + "?channel=" + Uri.EscapeDataString(_channel)));
    }

    private double DrawBackLink(Ui ui, double y)
    {
        var label = _name ?? _guid;
        var linkWidth = Fonts.Main.Measure(14, label) + 46;
        Item item = Chrome.Hit(ui, "shield-back", SideMargin - 6, y, linkWidth, 30);
        Canvas c = ui.Canvas()!;
        var color = item.Hovered ? Palette.Text : Palette.Muted;
        Icons.Back(c, item.X + 14, item.Y + 15, color);
        Chrome.Text(c, Fonts.Main, 14, item.X + 28, item.Y + 15, label, color);

        if (item.Clicked)
            OpenMod();

        return y + 30;
    }

    private double DrawHead(Ui ui, double width, double y)
    {
        Item head = Area(ui, "shield-head", SideMargin, y, width, 60);
        Canvas c = ui.Canvas()!;
        Widgets.PageHead(c, head.X, head.Y, _name ?? _guid);
        var scanned = _report.Str("scanned_at");
        var sub = (_reportVersion != null ? "v" + _reportVersion + " · " : "") + _channel + (scanned != null ? " · scanned " + DateText(scanned) : "");
        Chrome.Text(c, Fonts.Mono, 13, head.X, head.Y + 51, sub, Palette.Faint);
        return y + 60;
    }

    private double DrawWaiting(Ui ui, double width, double y)
    {
        if (!_failed)
        {
            ui.Pos(SideMargin + width / 2 - 12, y + 80);
            ui.Spinner(24);
            return y + 140;
        }

        Item box = Area(ui, "shield-error", SideMargin, y, 420, 110);
        Canvas c = ui.Canvas()!;
        Chrome.BoldText(c, Fonts.Main, 17, box.X, box.Y + 24, "Analysis unavailable", Palette.Text);
        Chrome.Text(c, Fonts.Main, 14.5, box.X, box.Y + 54, "No MonkeShield report could be loaded for this release.", Palette.Muted);
        return y + 110;
    }

    private double DrawSummary(Ui ui, double left, double width, double y)
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
        var height = Math.Max(220, 24 + 30 + 3 * 28 + 6 + diagLines.Sum(lines => lines.Count * 21 + 22) + hint.Count * 21 + 24);

        Item card = Area(ui, "shield-summary", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        DrawRing(c, card.X + 90, card.Y + 92, measured, risk, eligible);
        var pillWidth = Fonts.Main.Measure(12.5, verdict) + 18;
        Widgets.Pill(c, card.X + 90 - pillWidth / 2, card.Y + 166, verdict, eligible && measured ? Palette.Good : measured ? Palette.Bad : Palette.Muted);

        var x = card.X + textLeft;
        var top = card.Y + 24;
        Chrome.BoldText(c, Fonts.Main, 17, x, top + 10, heading, Palette.Text);
        top += 30;

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
}
