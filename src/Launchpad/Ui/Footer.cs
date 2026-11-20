using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.App;

namespace Launchpad.Ui;

using Ui = GoomeSharp.Ui;

public sealed class Footer(Shell shell)
{
    public const double Height = 99;

    private static readonly string[] Quips =
    [
        "Ever heard of MonkeModManager?",
        "Do NOT install OpSec from MonkeForge",
        "Ask me anything. I'll do everything.",
        "BBQ wings 10 o' clock",
        "ooo ooo aaa aaa",
        "Evan Burger's homepage",
    ];

    private static readonly (string Name, Route Target)[] Links =
    [
        ("Launchpad", new Route(Page.Software, "launchpad")),
        ("CLI", new Route(Page.Software, "cli")),
        ("OpenForge", new Route(Page.Software, "openforge")),
        ("Docs", new Route(Page.Docs)),
        ("Terms", new Route(Page.Legal, "terms")),
        ("Privacy", new Route(Page.Legal, "privacy")),
        ("Support MonkeForge", new Route(Page.Support)),
    ];

    private readonly string _quip = Quips[Random.Shared.Next(Quips.Length)];
    private readonly string _launchpad = typeof(LaunchpadApp).Assembly.GetName().Version?.ToString(3) ?? "2.0.0";
    private string? _forge;
    private bool _asked;

    public void Finish(Ui ui, string id, double bottom, double height, double width)
    {
        var top = Math.Max(bottom + 40, height - Height);
        Draw(ui, top, width);
        ui.Pos(0, top + Height);
        ui.Custom(id, 1, 1, ItemFlags.NoHit, out _);
    }

    public void Draw(Ui ui, double y, double width)
    {
        Ask();
        ui.Pos(0, y);
        ui.Custom("footer-box", width, Height, ItemFlags.NoHit, out Item box);
        Canvas c = ui.Canvas()!;
        c.DrawRect(box.X, box.Y, width, 1, Palette.Line);

        var top = box.Y + 35;
        c.DrawBitmap(Brand.Logo, box.X + 24, top - 9, 18, 18, 0, Palette.Faint);
        var x = box.X + 53;
        x += Chrome.BoldText(c, Fonts.Main, 14, x, top, "MonkeForge", Palette.Muted) + 24;
        x += Chrome.Text(c, Fonts.Mono, 11.5, x, top, _quip, Palette.Faint) + 18;
        Chip(c, x, top, "Launchpad v" + _launchpad);

        var right = width - 24;

        for (var i = Links.Length - 1; i >= 0; i--)
        {
            var linkWidth = Fonts.Main.Measure(12.5, Links[i].Name);
            right -= linkWidth;
            Link(ui, Links[i].Name, Links[i].Target, right, y + 35, linkWidth);
            right -= 18;
        }

        c = ui.Canvas()!;
        var second = box.Y + 67;
        x = box.X + 24;
        x += Chrome.Text(c, Fonts.Main, 12.5, x, second, "Community made. Not affiliated with Another Axiom.", Palette.Muted) + 28;

        if (_forge != null)
            Chip(c, x, second, "v" + _forge);
    }

    private void Link(Ui ui, string name, Route target, double x, double centerY, double width)
    {
        Item item = Chrome.Hit(ui, "footer-" + name, x - 2, centerY - 11, width + 4, 22);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 12.5, item.X + 2, item.Y + 11, name, item.Hovered ? Palette.Text : Palette.Muted);

        if (item.Clicked)
            shell.Router.Push(target);
    }

    private static void Chip(Canvas c, double x, double centerY, string text)
    {
        var width = Fonts.Mono.Measure(11.5, text) + 20;
        Chrome.Box(c, x, centerY - 10, width, 20, 5, Palette.Bg, Palette.LineStrong);
        Chrome.Text(c, Fonts.Mono, 11.5, x + 10, centerY, text, Palette.Faint);
    }

    private void Ask()
    {
        if (_asked)
            return;

        _asked = true;

        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode info = await shell.App.Forge.GetJsonAsync("/api/v2/version");
                _forge = info["openforge"]?.GetValue<string>();
            }
            catch (Exception)
            {
            }

            shell.Invalidate();
        });
    }
}
