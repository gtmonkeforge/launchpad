using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed class PlaceholderScreen(Shell shell, string title, string forgePath) : IScreen
{
    public void Draw(Ui ui, double width, double height)
    {
        var cardWidth = 420.0;
        var cardHeight = 190.0;
        var x = Math.Max(24, (width - cardWidth) / 2);
        var y = Math.Max(24, (height - cardHeight) / 3);

        ui.Pos(x, y);
        ui.Custom("placeholder-card", cardWidth, cardHeight, ItemFlags.NoHit, out Item card);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, cardWidth, cardHeight, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 20, card.X + 24, card.Y + 38, title, Palette.Text);

        var lines = Chrome.Wrap(Fonts.Main, 14.5, "This page isn't in Launchpad yet. You can use it on monkeforge.org for now.", cardWidth - 48, 3);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(c, Fonts.Main, 14.5, card.X + 24, card.Y + 74 + i * 22, lines[i], Palette.Muted);

        ui.Pos(x + 24, y + cardHeight - 62);
        ui.Size(220, 38);

        if (ui.Button("Open on monkeforge.org"))
            shell.OpenForge(forgePath);
    }

    public void Reload()
    {
    }
}
