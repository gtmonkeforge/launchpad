using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;
using static Launchpad.Ui.Screens.ModScreen;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ShieldScreen
{
    private double CheckHeight(string key, JsonNode check, double width)
    {
        if (!_openChecks.Contains(key))
            return CheckRow;

        var lines = WrapLines(Fonts.Main, 13.5, check.Str("explanation") ?? "", width - 40, 6).Count;
        var evidence = (check["evidence"] as JsonArray)?.Count ?? 0;
        var inconclusive = check.Str("status") == "Inconclusive" ? 1 : 0;
        return CheckRow + lines * 21 + evidence * 21 + inconclusive * 21 + 14;
    }

    private double DrawChecks(Ui ui, double left, double width, double y)
    {
        var sections = Sections(out var hidden);

        if (Artifacts.Count == 0)
            return DrawNoAssemblies(ui, left, width, y);

        foreach (var section in sections)
        {
            var heights = section.Checks.Select(item => CheckHeight(item.Key, item.Check, width - 44)).ToList();
            var body = section.Checks.Count == 0 ? 40 : heights.Sum();
            var height = 22 + 34 + body + 14;
            Item card = Area(ui, "shield-checks-" + section.Name, left, y, width, height);
            Canvas c = ui.Canvas()!;
            Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
            Chrome.BoldText(c, Fonts.Mono, 15, card.X + 22, card.Y + 34, section.Name, Palette.Text);
            var top = y + 56;

            if (section.Checks.Count == 0)
                Chrome.Text(c, Fonts.Main, 14, card.X + 22, card.Y + 56 + 20, _filter.Length > 0 || _onlyFailing ? "No checks match." : "No checks were reported for this assembly.", Palette.Muted);

            for (var i = 0; i < section.Checks.Count; i++)
            {
                DrawCheck(ui, section.Checks[i].Key, section.Checks[i].Check, left + 22, top, width - 44);
                top += heights[i];
            }

            y += height + 14;
        }

        if (hidden > 0 && Button(ui, "shield-checks-more", $"Show more ({hidden} left)", left, y, 200, 36, ButtonStyle.Plain))
            _checksShown += CheckBatch;

        return hidden > 0 ? y + 36 : y - 14;
    }

    private double DrawNoAssemblies(Ui ui, double left, double width, double y)
    {
        Item box = Area(ui, "shield-no-assemblies", left, y, width, 110);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, 110, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 17, box.X + 22, box.Y + 40, "No assemblies scanned", Palette.Text);
        Chrome.Text(c, Fonts.Main, 14, box.X + 22, box.Y + 70, "This release has not been analysed yet.", Palette.Muted);
        return y + 110;
    }

    private void DrawCheck(Ui ui, string key, JsonNode check, double x, double y, double width)
    {
        Item row = Chrome.Hit(ui, "shield-check-" + key, x, y, width, CheckRow);
        Canvas c = ui.Canvas()!;
        var failed = Failed(check);
        var open = _openChecks.Contains(key);
        c.DrawRect(row.X, row.Y, width, 1, Palette.Line);

        if (row.Hovered)
            c.DrawRect(row.X, row.Y + 1, width, CheckRow - 1, 0x0AFFFFFF);

        var label = Passing(check) ? "Pass" : failed ? "Fail" : "Not evaluated";
        var color = Passing(check) ? Palette.Good : failed ? Palette.Bad : Palette.Muted;
        var pillWidth = Fonts.Main.Measure(12.5, label) + 18;
        var textWidth = width - pillWidth - 40;
        Chrome.BoldText(c, Fonts.Main, 14, row.X + 6, row.Y + 17, Chrome.Clip(Fonts.Main, 14, check.Str("title") ?? "", textWidth), Palette.Text);
        Chrome.Text(c, Fonts.Mono, 12, row.X + 6, row.Y + 35, Chrome.Clip(Fonts.Mono, 12, $"{check.Str("ruleId")} · {check.Str("category")}", textWidth), Palette.Faint);
        Widgets.Pill(c, row.X + width - pillWidth - 24, row.Y + 12, label, color);
        c.DrawTriangle(row.X + width - 14, row.Y + 20, row.X + width - 6, row.Y + 20, row.X + width - 10, row.Y + 26, Palette.Faint);

        if (row.Clicked && !_openChecks.Remove(key))
            _openChecks.Add(key);

        if (!open)
            return;

        var top = row.Y + CheckRow;

        foreach (var line in WrapLines(Fonts.Main, 13.5, check.Str("explanation") ?? "", width - 40, 6))
        {
            Chrome.Text(c, Fonts.Main, 13.5, row.X + 6, top + 8, line, Palette.Muted);
            top += 21;
        }

        if (check.Str("status") == "Inconclusive")
        {
            Chrome.Text(c, Fonts.Main, 13.5, row.X + 6, top + 8, "Scanner result: inconclusive. Counted as passing.", Palette.Muted);
            top += 21;
        }

        foreach (var evidence in check["evidence"] as JsonArray ?? [])
        {
            var location = evidence.Str("location") ?? "";
            var used = Chrome.Text(c, Fonts.Mono, 12.5, row.X + 6, top + 8, location, Palette.Text);
            Chrome.Text(c, Fonts.Main, 13, row.X + 14 + used, top + 8, Chrome.Clip(Fonts.Main, 13, evidence.Str("detail") ?? "", width - used - 40), Palette.Muted);
            top += 21;
        }
    }

    private double DrawAssemblies(Ui ui, double left, double width, double y)
    {
        if (Artifacts.Count == 0)
            return DrawNoAssemblies(ui, left, width, y);

        for (var a = 0; a < Artifacts.Count; a++)
        {
            var report = Artifacts[a]?["report"];
            var diagnostics = (report?["diagnostics"] as JsonArray)?.Select(item => item?.GetValue<string>() ?? "").ToList() ?? [];
            var height = 22 + 40 + 4 * 32 + diagnostics.Count * 30 + 14;
            Item card = Area(ui, "shield-assembly-" + a, left, y, width, height);
            Canvas c = ui.Canvas()!;
            Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
            Chrome.BoldText(c, Fonts.Mono, 15, card.X + 22, card.Y + 34, Artifacts[a].Str("name") ?? "", Palette.Text);
            var size = report.Long("size");

            var rows = new (string Label, string Value)[]
            {
                ("Status", $"{report.Str("status")} · {report.Str("verdict") ?? "No verdict"}"),
                ("SHA-256", report.Str("sha256") ?? ""),
                ("Size", size > 0 ? Size(size) : "Unknown"),
                ("Scanner", report.Str("scannerVersion") ?? "Unknown"),
            };

            var top = card.Y + 62;

            foreach (var (label, value) in rows)
            {
                c.DrawRect(card.X + 22, top, width - 44, 1, Palette.Line);
                Chrome.Text(c, Fonts.Main, 13.5, card.X + 22, top + 16, label, Palette.Muted);
                Chrome.Text(c, Fonts.Mono, 12.5, card.X + 130, top + 16, Chrome.Clip(Fonts.Mono, 12.5, value, width - 170), Palette.Text);
                top += 32;
            }

            foreach (var text in diagnostics)
            {
                Chrome.Text(c, Fonts.Main, 13, card.X + 22, top + 14, Chrome.Clip(Fonts.Main, 13, text, width - 44), Palette.Warn);
                top += 30;
            }

            y += height + 14;
        }

        return y - 14;
    }
}
