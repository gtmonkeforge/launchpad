namespace Launchpad.Ui;

public enum Page
{
    Browse,
    Mod,
    Library,
    Publish,
    Admin,
    Settings,
    Game,
    SignIn,
}

public sealed record Route(Page Page, string? Arg = null);
