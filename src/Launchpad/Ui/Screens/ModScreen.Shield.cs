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

    private double DrawShield(Ui ui, double left, double width, double y)
    {
        var complete = _report.Str("status") == "complete";
        var total = _report.Long("total_checks");
        var failed = _report.Long("failed_checks");
        var measured = complete && total > 0;
        var risk = measured ? failed * 100.0 / total : 0;
        var eligible = _report.Bool("eligible");
        var verdict = complete ? (eligible ? "Passed" : "Review required") : StatusLabel(_report.Str("status"));
        var height = _report == null && !_reportFailed ? 130.0 : _report == null ? 110.0 : measured ? 301.0 : 271.0;

        Item card = Area(ui, "mod-shield", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Icons.Shield(c, card.X + 28, card.Y + 30, Palette.Muted);
        Chrome.BoldText(c, Fonts.Main, 15.5, card.X + 42, card.Y + 30, "MonkeShield", Palette.Text);

        if (_report == null)
        {
            DrawShieldWaiting(ui, card, left, y, width);
            return y + height;
        }

        DrawRing(c, card.X + width / 2, card.Y + 112, measured, risk, eligible);

        var pillWidth = Fonts.Main.Measure(12.5, verdict) + 18;
        Widgets.Pill(c, card.X + width / 2 - pillWidth / 2, card.Y + 183, verdict, eligible && measured ? Palette.Good : measured ? Palette.Bad : Palette.Muted);

        if (measured)
        {
            var counts = $"{failed} failed · {total - failed} passed";
            Chrome.Text(c, Fonts.Mono, 12.5, card.X + width / 2 - Fonts.Mono.Measure(12.5, counts) / 2, card.Y + 227, counts, Palette.Muted);
        }

        var buttonTop = measured ? y + 251 : y + 221;

        if (Button(ui, "mod-shield-report", "Full report", left + 20, buttonTop, width - 40, 30, ButtonStyle.Plain))
            OpenShield();

        return y + height;
    }

    private void DrawShieldWaiting(Ui ui, Item card, double left, double y, double width)
    {
        if (!_reportFailed)
        {
            ui.Pos(left + width / 2 - 10, y + 70);
            ui.Spinner(20);
            return;
        }

        Chrome.Text(ui.Canvas()!, Fonts.Main, 14, card.X + 20, card.Y + 64, "Analysis unavailable.", Palette.Muted);
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
