using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private const double ThumbWidth = 96;
    private const double ThumbHeight = 54;

    private int _shot;

    private double DrawGallery(Ui ui, double left, double width, double y, string[] shots)
    {
        var multiple = shots.Length > 1;
        var stage = Math.Min(380, (width - 2) * 9 / 16);
        var height = stage + (multiple ? ThumbHeight + 24 : 0);
        Item card = Area(ui, "mod-gallery", left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        _shot = Math.Clamp(_shot, 0, shots.Length - 1);

        c.ClipPush(card.X + 1, card.Y + 1, width - 2, stage);
        c.DrawRect(card.X + 1, card.Y + 1, width - 2, stage, Palette.Bg);
        DrawFitted(c, _shell.Images.Get(shots[_shot]), card.X + 1, card.Y + 1, width - 2, stage);
        c.ClipPop();

        if (multiple)
        {
            if (Arrow(ui, "mod-shot-prev", left + 14, y + stage / 2 - 20, true))
                _shot = (_shot - 1 + shots.Length) % shots.Length;

            if (Arrow(ui, "mod-shot-next", left + width - 54, y + stage / 2 - 20, false))
                _shot = (_shot + 1) % shots.Length;

            DrawThumbs(ui, left, y + stage + 12, width, shots);
        }

        return y + height;
    }

    private void DrawFitted(Canvas c, Bitmap? bitmap, double x, double y, double width, double height)
    {
        if (bitmap == null)
            return;

        var scale = Math.Min(width / bitmap.Width, height / bitmap.Height);
        var w = bitmap.Width * scale;
        var h = bitmap.Height * scale;
        c.DrawBitmap(bitmap, x + (width - w) / 2, y + (height - h) / 2, w, h);
    }

    private bool Arrow(Ui ui, string id, double x, double y, bool back)
    {
        Item item = Chrome.Hit(ui, id, x, y, 40, 40);
        Canvas c = ui.Canvas()!;
        c.DrawRRect(item.X, item.Y, 40, 40, 20, item.Hovered ? 0xE6000000 : 0xA6000000);

        if (back)
            Icons.Back(c, item.X + 20, item.Y + 20, Palette.Text);
        else
            Icons.Forward(c, item.X + 20, item.Y + 20, Palette.Text);

        return item.Clicked;
    }

    private void DrawThumbs(Ui ui, double left, double top, double width, string[] shots)
    {
        var x = left + 12;

        for (var i = 0; i < shots.Length && x + ThumbWidth <= left + width - 12; i++)
        {
            Item thumb = Chrome.Hit(ui, "mod-thumb-" + i, x, top, ThumbWidth, ThumbHeight);
            Canvas c = ui.Canvas()!;
            c.DrawRRect(thumb.X, thumb.Y, ThumbWidth, ThumbHeight, 8, i == _shot ? Palette.Accent : Palette.Line);
            c.ClipPush(thumb.X + 2, thumb.Y + 2, ThumbWidth - 4, ThumbHeight - 4);
            c.DrawRect(thumb.X + 2, thumb.Y + 2, ThumbWidth - 4, ThumbHeight - 4, Palette.Bg);
            DrawFitted(c, _shell.Images.Get(shots[i]), thumb.X + 2, thumb.Y + 2, ThumbWidth - 4, ThumbHeight - 4);
            c.ClipPop();

            if (thumb.Clicked)
                _shot = i;

            x += ThumbWidth + 8;
        }
    }
}
