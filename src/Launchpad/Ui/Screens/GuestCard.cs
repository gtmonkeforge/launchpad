using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public static class GuestCard
{
    private const double Width = 460;

    public static void Draw(Ui ui, Shell shell, string id, double width, double height, string title, string text, string signIn, bool stacked)
    {
        var lines = Chrome.Wrap(Fonts.Main, 14.5, text, Width - 64, 4);
        var tall = 187 + lines.Count * 23 + (stacked ? 102 : 52);
        var x = Math.Max(24, (width - Width) / 2);
        var y = Palette.TitlebarHeight + Math.Max(24, Math.Min(80, (height - Footer.Height - tall) / 2));

        ui.Pos(x, y);
        ui.Custom(id + "-card", Width, tall, ItemFlags.NoHit, out Item card);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, Width, tall, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        c.DrawBitmap(Brand.Logo, card.X + Width / 2 - 20, card.Y + 38, 40, 40, 0, Palette.Accent);
        Chrome.BoldText(c, Fonts.Main, 22, card.X + Width / 2 - Fonts.Main.Measure(22, title) / 2, card.Y + 113, title, Palette.Text);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14.5, card.X + Width / 2 - Fonts.Main.Measure(14.5, lines[i]) / 2, card.Y + 157 + i * 23, lines[i], Palette.Muted);

        var top = y + 161 + lines.Count * 23;

        if (shell.SigningIn)
        {
            Waiting(ui, shell, id, x, top);
            return;
        }

        var a = Widgets.ButtonWidth(signIn, true);
        var b = Widgets.ButtonWidth("Create an account");
        var left = x + Width / 2 - (stacked ? a / 2 : (a + b + 8) / 2);

        if (Widgets.Button(ui, id + "-signin", left, top, signIn, ButtonKind.Primary, out _, true, 42, Icons.User))
            shell.SignIn();

        var createX = stacked ? x + Width / 2 - b / 2 : left + a + 8;

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
