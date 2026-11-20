using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SupportScreen : IScreen
{
    private const double MostWidth = 860;

    private readonly Shell _shell;
    private readonly string? _arg;
    private int _request;
    private JsonNode? _data;
    private string? _error;
    private bool _started;

    public SupportScreen(Shell shell, string? arg)
    {
        _shell = shell;
        _arg = arg;
    }

    public void Reload() => _started = false;

    public void Draw(Ui ui, double width, double height)
    {
        PageFrame.Begin(ui, "support", height);
        var column = PageFrame.ColumnWidth(width, MostWidth);
        var left = PageFrame.ColumnLeft(width, column);
        var bottom = DrawView(ui, left, column);
        PageFrame.End(ui, _shell, "support", bottom, height, width);
    }

    private double DrawHelp(Ui ui, double left, double width)
    {
        const string helpText = "The docs cover installing mods, publishing, payouts and the game loader. Still stuck? Ask in the community channel where you found MonkeForge, or email the address on the website.";
        const string donateText = "MonkeForge is community made and runs on donations. If it saved you time, you can chip in on the website. Donating never buys anything.";
        PageFrame.Title(ui, "support-title", left, 28, "Support", "Get help, or help keep MonkeForge running.");
        var helpHeight = 118 + Card.HintHeight(helpText, width);
        Card help = Card.Open(ui, "support-help", left, 108, width, helpHeight, "Need a hand?");
        help.Hint(0, 46, width, helpText);

        if (Widgets.Button(ui, "support-docs", left + Card.Padding, 108 + helpHeight - 54, "Read the docs", ButtonKind.Primary, out var docsWidth, true, 36))
            _shell.Router.Push(new Route(Page.Docs));

        if (Widgets.Button(ui, "support-software", left + Card.Padding + docsWidth + 10, 108 + helpHeight - 54, "Get the apps", ButtonKind.Subtle, out _, true, 36))
            _shell.Router.Push(new Route(Page.Software));

        var donateTop = 108 + helpHeight + 16;
        var donateHeight = 118 + Card.HintHeight(donateText, width);
        Card donate = Card.Open(ui, "support-donate", left, donateTop, width, donateHeight, "Support MonkeForge");
        donate.Hint(0, 46, width, donateText);

        if (Widgets.Button(ui, "support-donate-button", left + Card.Padding, donateTop + donateHeight - 54, "Donate on monkeforge.org", ButtonKind.Primary, out _, true, 36))
            _shell.OpenForge("/");

        return donateTop + donateHeight;
    }

    private double DrawView(Ui ui, double left, double width)
    {
        if (!_shell.IsAdmin)
            return DrawHelp(ui, left, width);

        if (_arg == "new")
            return DrawCompose(ui, left, width);

        return long.TryParse(_arg, out var id) ? DrawThread(ui, left, width, id) : DrawList(ui, left, width);
    }

    private void Fetch(string path)
    {
        _started = true;
        _data = null;
        _error = null;
        var request = ++_request;

        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode result = await _shell.App.Forge.GetJsonAsync(path);

                if (request == _request)
                    _data = result;
            }
            catch (UserFacingException ex)
            {
                if (request == _request)
                    _error = ex.Message;
            }

            _shell.Invalidate();
        });
    }

    private void Send(HttpMethod method, string path, object body, string done, Action<JsonNode>? finished = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                JsonNode result = await _shell.App.Forge.SendJsonAsync(method, path, body);

                if (done.Length > 0)
                    _shell.Toast(done);

                finished?.Invoke(result);
            }
            catch (UserFacingException ex)
            {
                _shell.Toast(ex.Message);
            }

            _started = false;
            _shell.Invalidate();
        });
    }

    private bool Waiting(Ui ui, double left, double width, double top, out double bottom)
    {
        bottom = top + 120;

        if (_error != null)
        {
            Card card = Card.Open(ui, "support-error", left, top, width, 100, "Couldn't load this page");
            card.Hint(0, 46, width, _error, Palette.Bad);
            return true;
        }

        if (_data == null)
        {
            ui.Pos(left + width / 2 - 12, top + 60);
            ui.Spinner(24);
            return true;
        }

        return false;
    }

    private static uint StatusColor(string status) => status switch
    {
        "open" => Palette.Good,
        "pending" => Palette.Warn,
        _ => Palette.Muted,
    };
}
