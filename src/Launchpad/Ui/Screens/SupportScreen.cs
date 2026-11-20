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

    public void Reload()
    {
        _started = false;
    }

    public void Draw(Ui ui, double width, double height)
    {
        PageFrame.Begin(ui, "support", height);
        var column = PageFrame.ColumnWidth(width, MostWidth);
        var left = PageFrame.ColumnLeft(width, column);
        var bottom = DrawView(ui, left, column);
        PageFrame.End(ui, _shell, "support", bottom, height, width);
    }

    private double DrawView(Ui ui, double left, double width)
    {
        if (!_shell.IsAdmin)
            return DrawHelp(ui, left, width);

        if (_arg == "new")
            return DrawCompose(ui, left, width);

        if (_arg != null && long.TryParse(_arg, out var id))
            return DrawThread(ui, left, width, id);

        return DrawList(ui, left, width);
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
