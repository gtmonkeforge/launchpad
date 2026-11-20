using Launchpad.Ui.Screens;

namespace Launchpad.Ui;

public sealed class Router(Shell shell)
{
    private readonly List<(Route Route, IScreen Screen)> _stack = [];
    private const int HistoryLimit = 40;

    private int _at = -1;

    public Route Current => _stack[_at].Route;
    public IScreen Screen => _stack[_at].Screen;
    public bool CanGoBack => _at > 0;
    public bool CanGoForward => _at < _stack.Count - 1;

    public void Push(Route route)
    {
        if (_at >= 0 && Current == route)
            return;

        if (_at >= 0)
            Screen.Leave();

        Drop(_at + 1, _stack.Count - _at - 1);
        _stack.Add((route, Create(route)));
        _at++;

        if (_stack.Count > HistoryLimit)
        {
            Drop(0, 1);
            _at--;
        }

        shell.Invalidate();
    }

    public void Back()
    {
        if (!CanGoBack)
            return;

        Screen.Leave();
        _at--;
    }

    public void Forward()
    {
        if (!CanGoForward)
            return;

        Screen.Leave();
        _at++;
    }

    private void Drop(int start, int count)
    {
        for (var i = start; i < start + count; i++)
            _stack[i].Screen.Dispose();

        _stack.RemoveRange(start, count);
    }

    public void Reload() => Screen.Reload();

    private IScreen Create(Route route) => route.Page switch
    {
        Page.Browse => new BrowseScreen(shell),
        Page.Mod => new ModScreen(shell, route.Arg),
        Page.Library => new LibraryScreen(shell),
        Page.Publish => new PublishScreen(shell),
        Page.Admin => new AdminScreen(shell),
        Page.Game => new GameSettingsScreen(shell, route.Arg),
        Page.Settings => new SettingsScreen(shell, route.Arg),
        Page.SignIn => new SignInScreen(shell),
        Page.Docs => new DocsScreen(shell, route.Arg),
        Page.Legal => new LegalScreen(shell, route.Arg),
        Page.Software => new SoftwareScreen(shell, route.Arg),
        Page.Support => new SupportScreen(shell, route.Arg),
        Page.Profile => new ProfileScreen(shell, route.Arg),
        Page.Manage => new ManageScreen(shell, route.Arg),
        Page.Shield => new ShieldScreen(shell, route.Arg),
        _ => new PlaceholderScreen(shell, "Launchpad", "/"),
    };
}
