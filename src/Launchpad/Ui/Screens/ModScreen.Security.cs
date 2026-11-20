using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private void OpenShield()
    {
        _shell.Router.Push(new Route(Page.Shield, _guid + "?channel=" + Uri.EscapeDataString(_channel)));
    }

    private double DrawSecurity(Ui ui, double left, double width, double y)
    {
        if (_report == null)
            return DrawSecurityWaiting(ui, left, width, y);

        y = DrawSecuritySummary(ui, left, width, y);
        Item hint = Area(ui, "mod-security-hint", left, y + 14, width, 24);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 13, hint.X, hint.Y + 12, ShieldHint, Palette.Faint);
        return y + 42;
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
        var (measured, risk, eligible, verdict, color) = ShieldState(_report);
        var total = _report.Long("total_checks");
        var failed = _report.Long("failed_checks");
        var textWidth = width - 44;
        var policy = Chrome.Wrap(Fonts.Main, 13.5, $"Static analysis of every .NET assembly in the latest {_channel} release. Automatic approval requires fewer than 2.5% failed checks in each assembly.", textWidth, 3);
        var diagLines = Diagnostics(_report).Select(text => Chrome.Wrap(Fonts.Main, 13.5, text, textWidth - 28, 3)).ToList();
        var height = 188 + policy.Count * 21 + (measured ? 28 : 0) + 12 + 52 + diagLines.Sum(lines => lines.Count * 21 + 22) + 6;

        Item card = Area(ui, "mod-security", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        DrawRing(c, card.X + width / 2, card.Y + 82, measured, risk, eligible);
        Widgets.Pill(c, card.X + width / 2 - (Fonts.Main.Measure(12.5, verdict) + 18) / 2, card.Y + 156, verdict, color);

        var x = card.X + 22;
        var top = card.Y + 188;

        foreach (var line in policy)
        {
            Chrome.Text(c, Fonts.Main, 13.5, x, top + 10, line, Palette.Muted);
            top += 21;
        }

        if (measured)
        {
            Chrome.Text(c, Fonts.Mono, 12.5, x, top + 22, $"{failed} failed · {total - failed} passed", Palette.Muted);
            top += 28;
        }

        top += 12;

        if (Button(ui, "mod-security-open", "Open full report", left + 22, y + top - card.Y, 170, 36, ButtonStyle.Plain, true, (canvas, gx, gy, color) => Icons.Shield(canvas, gx, gy, color)))
            OpenShield();

        top += 52;

        DrawDiagnostics(ui.Canvas()!, x, top, textWidth, diagLines);

        return y + height;
    }
}
