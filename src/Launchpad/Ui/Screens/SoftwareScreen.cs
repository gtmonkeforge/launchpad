using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;
using Launchpad.App;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SoftwareScreen : IScreen
{
    private const double MostWidth = 900;

    private static readonly string[] Keys = ["", "launchpad", "cli", "openforge"];
    private static readonly string[] Labels = ["All software", "Launchpad", "mforge", "OpenForge"];

    private static readonly (string Key, string Name, string Text, string Platforms)[] Products =
    [
        ("launchpad", "Launchpad", "Desktop app for installing mods and launching Gorilla Tag.", "Windows, Linux"),
        ("cli", "mforge", "Command-line tool for installing and upgrading mods.", "Windows, Linux"),
        ("openforge", "OpenForge", "The server behind this site. Open source, written in C#.", "Self-hosted"),
    ];

    private readonly Shell _shell;
    private readonly int _tab;
    private readonly string _launchpadVersion = typeof(LaunchpadApp).Assembly.GetName().Version?.ToString(3) ?? "2.0.0";
    private string? _forgeVersion;
    private bool _asked;
    private int _count;

    public SoftwareScreen(Shell shell, string? arg)
    {
        _shell = shell;
        _tab = Math.Max(0, Array.IndexOf(Keys, arg ?? ""));
    }

    public void Reload()
    {
        _asked = false;
    }

    public void Draw(Ui ui, double width, double height)
    {
        Ask();
        _count = 0;
        PageFrame.Begin(ui, "software", height);
        var column = PageFrame.ColumnWidth(width, MostWidth);
        var left = PageFrame.ColumnLeft(width, column);

        var clicked = PageFrame.Tabs(ui, "software-tab-", left, 28, Labels, _tab);

        if (clicked >= 0 && clicked != _tab)
            _shell.Router.Push(new Route(Page.Software, Keys[clicked].Length == 0 ? null : Keys[clicked]));

        var y = 84.0;

        y = _tab switch
        {
            1 => DrawLaunchpad(ui, left, column, y),
            2 => DrawCli(ui, left, column, y),
            3 => DrawOpenForge(ui, left, column, y),
            _ => DrawHub(ui, left, column, y),
        };

        PageFrame.End(ui, "software", y);
    }

    private double DrawHub(Ui ui, double left, double width, double y)
    {
        y = Header(ui, left, width, y, "Software", "Tools for using MonkeForge outside the browser, and the server that runs it.", []);

        foreach (var product in Products)
        {
            Item row = Chrome.Hit(ui, "software-row-" + product.Key, left, y, width, 74);
            Canvas c = ui.Canvas()!;
            Chrome.Box(c, row.X, row.Y, width, 74, Palette.RadiusLarge, row.Hovered ? Palette.Surface2 : Palette.Surface, Palette.Line);
            Chrome.BoldText(c, Fonts.Main, 16, row.X + 22, row.Y + 26, product.Name, Palette.Text);
            Chrome.Text(c, Fonts.Main, 14, row.X + 22, row.Y + 51, Chrome.Clip(Fonts.Main, 14, product.Text, width - 220), Palette.Muted);
            var platforms = product.Platforms;
            Chrome.Text(c, Fonts.Main, 13.5, row.X + width - 22 - Fonts.Main.Measure(13.5, platforms), row.Y + 37, platforms, Palette.Faint);

            if (row.Clicked)
                _shell.Router.Push(new Route(Page.Software, product.Key));

            y += 84;
        }

        return y;
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
                JsonNode info = await _shell.App.Forge.GetJsonAsync("/api/v2/version");
                _forgeVersion = info["openforge"]?.GetValue<string>();
            }
            catch (Exception)
            {
            }

            _shell.Invalidate();
        });
    }
}
