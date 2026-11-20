
namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed class AdminScreen(Shell shell) : IScreen
{
    private const double CardWidth = 480;

    public void Draw(Ui ui, double width, double height)
    {
        var (title, text, button) = Wording();
        var textHeight = Card.HintHeight(text, CardWidth);
        var cardHeight = 116 + textHeight;
        var x = Math.Max(24, (width - CardWidth) / 2);
        var y = Math.Max(24, (height - cardHeight) / 3);

        Card card = Card.Open(ui, "admin-card", x, y, CardWidth, cardHeight, title);
        card.Hint(0, 46, CardWidth, text);

        if (!Card.Button(ui, button, x + Card.Padding, y + cardHeight - 56, 360))
            return;

        if (!shell.SignedIn)
            shell.Router.Push(new Route(Page.SignIn));
        else if (shell.IsAdmin)
            shell.OpenForge("/admin");
        else
            shell.Router.Push(new Route(Page.Browse));
    }

    public void Reload()
    {
    }

    private (string Title, string Text, string Button) Wording()
    {
        if (!shell.SignedIn)
            return ("Admin", "Sign in with a staff account to moderate mods, users and reports.", "Sign in");

        if (!shell.IsAdmin)
            return ("Admin", "Your account isn't a staff account, so there is nothing to moderate here.", "Back to mods");

        return ("Admin", "Moderation tools live on the website. Open the admin panel in your browser.", "Open the admin panel on monkeforge.org");
    }
}
