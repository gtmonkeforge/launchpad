using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed class SignInScreen(Shell shell) : IScreen
{
    private const double CardWidth = 420;
    private const double CardHeight = 230;

    public void Draw(Ui ui, double width, double height)
    {
        if (shell.SignedIn && !shell.SigningIn)
        {
            shell.Router.Push(new Route(Page.Browse));
            return;
        }

        var x = Math.Max(24, (width - CardWidth) / 2);
        var y = Math.Max(24, (height - CardHeight) / 3);

        ui.Pos(x, y);
        ui.Custom("signin-card", CardWidth, CardHeight, ItemFlags.NoHit, out Item card);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, CardWidth, CardHeight, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 20, card.X + 24, card.Y + 38, "Sign in", Palette.Text);

        var text = shell.SigningIn
            ? "Finish signing in in your browser. Launchpad will pick it up from there."
            : "Sign in to publish mods, rate, comment and sync settings.";
        var lines = Chrome.Wrap(Fonts.Main, 14.5, text, CardWidth - 48, 3);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14.5, card.X + 24, card.Y + 74 + i * 22, lines[i], Palette.Muted);

        ui.Pos(x + 24, y + CardHeight - 62);

        if (shell.SigningIn)
        {
            ui.Size(36, 38);
            ui.Spinner(24);
            ui.Pos(x + 72, y + CardHeight - 62);
            ui.Size(110, 38);

            if (ui.Button("Cancel"))
                shell.CancelSignIn();
        }
        else
        {
            ui.Size(220, 38);

            if (ui.Button("Sign in with MonkeForge"))
                shell.SignIn();
        }
    }

    public void Reload()
    {
    }
}
