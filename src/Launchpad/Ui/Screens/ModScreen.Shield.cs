using System.Globalization;
using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private JsonNode? _report;
    private bool _reportFailed;
    private string? _reportVersion;
    private string _rating = "-";

    private void LoadExtras(string guid)
    {
        _report = null;
        _reportFailed = false;
        var basePath = "/api/v1/mods/" + Uri.EscapeDataString(guid);
        var request = _request;

        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode result = await _shell.App.Forge.GetJsonAsync(basePath + "/monkeshield-report?channel=" + Uri.EscapeDataString(_channel));

                if (request == _request)
                {
                    _report = result["report"];
                    _reportVersion = result.Str("version");
                }
            }
            catch (UserFacingException)
            {
                _reportFailed = true;
            }

            _shell.Invalidate();
        });

        LoadCommunity();
    }

    internal const string ShieldHint = "Static analysis is not a guarantee of safety. Reports reflect the latest release in this channel.";

    internal static (bool Measured, double Risk, bool Eligible, string Verdict, uint Color) ShieldState(JsonNode? report)
    {
        var complete = report.Str("status") == "complete";
        var total = report.Long("total_checks");
        var measured = complete && total > 0;
        var risk = measured ? report.Long("failed_checks") * 100.0 / total : 0;
        var eligible = report.Bool("eligible");
        var verdict = complete ? (eligible ? "Passed" : "Review required") : StatusLabel(report.Str("status"));
        var color = eligible && measured ? Palette.Good : measured ? Palette.Bad : Palette.Muted;
        return (measured, risk, eligible, verdict, color);
    }

    internal static List<string> Diagnostics(JsonNode? report) => (report?["diagnostics"] as JsonArray)?.Select(item => item?.GetValue<string>() ?? "").ToList() ?? [];

    internal static double DrawDiagnostics(Canvas c, double x, double top, double width, List<List<string>> diagLines)
    {
        foreach (var lines in diagLines)
        {
            var boxHeight = lines.Count * 21 + 14;
            Chrome.Box(c, x, top, width, boxHeight, Palette.Radius, 0x14E5B85C, 0x5CE5B85C);

            for (var i = 0; i < lines.Count; i++)
                Chrome.Text(c, Fonts.Main, 13.5, x + 14, top + 17 + i * 21, lines[i], Palette.Warn);

            top += boxHeight + 8;
        }

        return top;
    }

    private double DrawShield(Ui ui, double left, double width, double y)
    {
        var (measured, risk, eligible, verdict, color) = ShieldState(_report);
        var failed = _report.Long("failed_checks");
        var total = _report.Long("total_checks");
        var height = _report == null && !_reportFailed ? 130.0 : _report == null ? 110.0 : measured ? 301.0 : 271.0;

        Item card = Area(ui, "mod-shield", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Icons.Shield(c, card.X + 28, card.Y + 30, Palette.Muted);
        Chrome.BoldText(c, Fonts.Main, 15.5, card.X + 42, card.Y + 30, "MonkeShield", Palette.Text);

        if (_report == null)
        {
            if (!_reportFailed)
            {
                ui.Pos(left + width / 2 - 10, y + 70);
                ui.Spinner(20);
            }
            else
                Chrome.Text(ui.Canvas()!, Fonts.Main, 14, card.X + 20, card.Y + 64, "Analysis unavailable.", Palette.Muted);

            return y + height;
        }

        DrawRing(c, card.X + width / 2, card.Y + 112, measured, risk, eligible);
        Widgets.Pill(c, card.X + width / 2 - (Fonts.Main.Measure(12.5, verdict) + 18) / 2, card.Y + 183, verdict, color);

        if (measured)
        {
            var counts = $"{failed} failed · {total - failed} passed";
            Chrome.Text(c, Fonts.Mono, 12.5, card.X + width / 2 - Fonts.Mono.Measure(12.5, counts) / 2, card.Y + 227, counts, Palette.Muted);
        }

        if (Button(ui, "mod-shield-report", "Full report", left + 20, measured ? y + 251 : y + 221, width - 40, 30, ButtonStyle.Plain))
            OpenShield();

        return y + height;
    }

    internal static void DrawRing(Canvas c, double x, double y, bool measured, double risk, bool eligible)
    {
        const double radius = 51;
        c.DrawRing(x, y, radius, 7, Palette.Line);
        var color = !measured ? Palette.LineStrong : risk < 15 ? Palette.Good : Palette.Bad;
        var filled = measured ? Math.Min(1, (100 - risk) / 100) : 1;
        var start = -Math.PI / 2;

        if (filled >= 0.999)
            c.DrawRing(x, y, radius, 7, color);
        else
            c.DrawArc(x, y, radius, 7, start, start + Math.PI * 2 * filled, color);

        var text = measured ? risk.ToString("0.00", CultureInfo.InvariantCulture) + "%" : "-";
        Chrome.BoldText(c, Fonts.Mono, 20, x - Fonts.Mono.Measure(20, text) / 2, y - 3, text, Palette.Text);
        Chrome.Text(c, Fonts.Main, 11, x - Fonts.Main.Measure(11, "risk") / 2, y + 16, "risk", Palette.Faint);
    }

    internal static string StatusLabel(string? status) => status switch
    {
        "scanning" => "Scan in progress",
        "not_scanned" => "Not scanned yet",
        "incomplete" => "Scan incomplete",
        "unavailable" => "Scanner unavailable",
        _ => "Unavailable",
    };
}
