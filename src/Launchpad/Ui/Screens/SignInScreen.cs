using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed class SignInScreen(Shell shell) : IScreen
{
    public void Draw(Ui ui, double width, double height)
    {
        if (shell.SignedIn && !shell.SigningIn)
        {
            shell.Router.Push(new Route(Page.Browse));
            return;
        }

        var text = shell.SigningIn
            ? "Finish signing in in your browser. Launchpad will pick it up from there."
            : "Your browser opens monkeforge.org, where you sign in with Discord, a passkey, or your password. Launchpad picks it up from there.";

        GuestCard.Draw(ui, shell, "signin", width, height, "Sign in to MonkeForge", text, "Sign in with your browser", true);
        shell.Footer.Draw(ui, Palette.TitlebarHeight + height - Footer.Height, width);
    }

    public void Reload()
    {
    }
}
