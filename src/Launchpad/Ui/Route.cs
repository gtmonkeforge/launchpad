namespace Launchpad.Ui;

public enum Page
{
    Browse,
    Mod,
    Library,
    Publish,
    Admin,
    Settings,
    SignIn,
}

public sealed record Route(Page Page, string? Arg = null);
