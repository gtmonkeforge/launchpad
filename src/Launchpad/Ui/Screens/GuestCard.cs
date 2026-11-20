using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public static class GuestCard
{
    private const double Width = 460;

    public static void Draw(Ui ui, Shell shell, string id, double width, double height, string title, string text, string signIn, bool stacked)
    {
        var lines = Chrome.Wrap(Fonts.Main, 14.5, text, Width - 64, 4);
        var cardHeight = 205 + lines.Count * 23 + (stacked ? 102 : 52);
        var x = Math.Max(24, (width - Width) / 2);
        var y = Palette.TitlebarHeight + Math.Max(24, Math.Min(80, (height - Footer.Height - cardHeight) / 2));

        ui.Pos(x, y);
        ui.Custom(id + "-card", Width, cardHeight, ItemFlags.NoHit, out Item card);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, Width, cardHeight, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        c.DrawBitmap(Brand.Logo, card.X + Width / 2 - 20, card.Y + 38, 40, 40, 0, Palette.Accent);
        Chrome.BoldText(c, Fonts.Main, 22, card.X + Width / 2 - Fonts.Main.Measure(22, title) / 2, card.Y + 112, title, Palette.Text);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14.5, card.X + Width / 2 - Fonts.Main.Measure(14.5, lines[i]) / 2, card.Y + 153 + i * 23, lines[i], Palette.Muted);

        var top = y + 176 + lines.Count * 23;

        if (shell.SigningIn)
        {
            Waiting(ui, shell, id, x, top);
            return;
        }

        var signInWidth = Widgets.ButtonWidth(signIn, true);
        var createWidth = Widgets.ButtonWidth("Create an account");

        if (Widgets.Button(ui, id + "-signin", x + Width / 2 - (stacked ? signInWidth / 2 : (signInWidth + createWidth + 8) / 2), top, signIn, ButtonKind.Primary, out _, true, 42, Icons.User))
            shell.SignIn();

        var createX = stacked ? x + Width / 2 - createWidth / 2 : x + Width / 2 - (signInWidth + createWidth + 8) / 2 + signInWidth + 8;

        if (Widgets.Button(ui, id + "-create", createX, stacked ? top + 50 : top, "Create an account", ButtonKind.Subtle, out _, true, 42))
            shell.OpenForge("/signup");
    }

    private static void Waiting(Ui ui, Shell shell, string id, double x, double top)
    {
        ui.Pos(x + Width / 2 - 70, top + 9);
        ui.Size(24, 24);
        ui.Spinner(24);

        if (Widgets.Button(ui, id + "-cancel", x + Width / 2 - 30, top, "Cancel", ButtonKind.Subtle, out _, true, 42))
            shell.CancelSignIn();
    }
}
