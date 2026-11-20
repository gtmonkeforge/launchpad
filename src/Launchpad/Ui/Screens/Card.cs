using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed class Card
{
    public const double Padding = 22;
    public const double LineHeight = 21;

    private readonly Canvas _canvas;
    private readonly double _x;
    private readonly double _y;

    private Card(Canvas canvas, double x, double y)
    {
        _canvas = canvas;
        _x = x;
        _y = y;
    }

    public static Card Open(Ui ui, string id, double x, double y, double width, double height, string title)
    {
        ui.Pos(x, y);
        ui.Custom(id, width, height, ItemFlags.NoHit, out Item box);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        var card = new Card(c, box.X, box.Y);
        card.Bold(Padding, 30, 17, title, Palette.Text);
        return card;
    }

    public double Hint(double indent, double top, double width, string text, uint color = Palette.Muted)
    {
        var lines = Chrome.Wrap(Fonts.Main, 14, text, width - Padding * 2, 8);

        for (var i = 0; i < lines.Count; i++)
            Chrome.Text(_canvas, Fonts.Main, 14, _x + Padding + indent, _y + top + i * LineHeight + LineHeight / 2, lines[i], color);

        return lines.Count * LineHeight;
    }

    public void Text(double left, double centerY, double size, string text, uint color, bool mono = false) =>
        Chrome.Text(_canvas, mono ? Fonts.Mono : Fonts.Main, size, _x + left, _y + centerY, text, color);

    public void Bold(double left, double centerY, double size, string text, uint color) =>
        Chrome.BoldText(_canvas, Fonts.Main, size, _x + left, _y + centerY, text, color);

    public void Avatar(Bitmap? picture, double left, double top, double size, string name)
    {
        if (picture != null)
        {
            _canvas.DrawBitmap(picture, _x + left, _y + top, size, size, size / 2);
            return;
        }

        _canvas.DrawCircle(_x + left + size / 2, _y + top + size / 2, size / 2, Palette.Surface2);
        var letters = Format.Initials(name);
        Chrome.Text(_canvas, Fonts.Main, 16, _x + left + size / 2 - Fonts.Main.Measure(16, letters) / 2, _y + top + size / 2, letters, Palette.Text);
    }

    public void PillRight(double right, double top, string text, uint color)
    {
        var width = Fonts.Main.Measure(12.5, text) + 18;
        Widgets.Pill(_canvas, _x + right - width, _y + top, text, color);
    }

    public double Pill(double left, double top, string text, uint color) => Widgets.Pill(_canvas, _x + left, _y + top, text, color);

    public static double HintHeight(string text, double width) => Chrome.Wrap(Fonts.Main, 14, text, width - Padding * 2, 8).Count * LineHeight;

    public static bool Button(Ui ui, string text, double x, double y, double width)
    {
        ui.Pos(x, y);
        ui.Size(width, 36);
        return ui.Button(text);
    }
}
