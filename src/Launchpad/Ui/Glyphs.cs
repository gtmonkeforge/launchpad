using GoomeSharp;

namespace Launchpad.Ui;

public static class Glyphs
{
    public static void Trash(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x - 5, y - 3.5, x + 5, y - 3.5, 1.3, color);
        c.DrawLine(x - 1.5, y - 5.5, x + 1.5, y - 5.5, 1.3, color);
        c.DrawLine(x - 3.5, y - 3, x - 3, y + 5, 1.3, color);
        c.DrawLine(x + 3.5, y - 3, x + 3, y + 5, 1.3, color);
        c.DrawLine(x - 3, y + 5, x + 3, y + 5, 1.3, color);
    }

    public static void Link(Canvas c, double x, double y, uint color)
    {
        c.DrawRRectLine(x - 6, y - 3, 7, 6, 3, 1.3, color);
        c.DrawRRectLine(x - 1, y - 3, 7, 6, 3, 1.3, color);
    }

    public static void Code(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x - 2, y - 4, x - 6, y, 1.4, color);
        c.DrawLine(x - 6, y, x - 2, y + 4, 1.4, color);
        c.DrawLine(x + 2, y - 4, x + 6, y, 1.4, color);
        c.DrawLine(x + 6, y, x + 2, y + 4, 1.4, color);
    }

    public static void Game(Canvas c, double x, double y, uint color)
    {
        c.DrawRRectLine(x - 7, y - 4, 14, 8, 3.5, 1.3, color);
        c.DrawLine(x - 4, y - 1.5, x - 4, y + 1.5, 1.2, color);
        c.DrawLine(x - 5.5, y, x - 2.5, y, 1.2, color);
        c.DrawCircle(x + 3, y - 1, 0.9, color);
        c.DrawCircle(x + 5, y + 1, 0.9, color);
    }

    public static void Layers(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x, y - 6, x + 6.5, y - 2.5, 1.3, color);
        c.DrawLine(x + 6.5, y - 2.5, x, y + 1, 1.3, color);
        c.DrawLine(x, y + 1, x - 6.5, y - 2.5, 1.3, color);
        c.DrawLine(x - 6.5, y - 2.5, x, y - 6, 1.3, color);
        c.DrawLine(x - 6.5, y + 1, x, y + 4.5, 1.3, color);
        c.DrawLine(x, y + 4.5, x + 6.5, y + 1, 1.3, color);
    }

    public static void Rocket(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x + 6, y - 6, x - 2, y + 2, 1.5, color);
        c.DrawLine(x + 6, y - 6, x + 5, y - 1, 1.3, color);
        c.DrawLine(x + 6, y - 6, x + 1, y - 5, 1.3, color);
        c.DrawLine(x - 6, y + 6, x - 4, y + 3, 1.3, color);
        c.DrawCircle(x + 1, y - 1, 1.2, color);
    }

    public static void Gear(Canvas c, double x, double y, uint color)
    {
        c.DrawRing(x, y, 5.5, 1.3, color);
        c.DrawRing(x, y, 1.8, 1.2, color);

        for (var i = 0; i < 8; i++)
        {
            var a = i * Math.PI / 4;
            c.DrawLine(x + Math.Cos(a) * 5.5, y + Math.Sin(a) * 5.5, x + Math.Cos(a) * 7.5, y + Math.Sin(a) * 7.5, 1.4, color);
        }
    }

    public static void Server(Canvas c, double x, double y, uint color)
    {
        c.DrawRRectLine(x - 6.5, y - 6.5, 13, 5.5, 2, 1.3, color);
        c.DrawRRectLine(x - 6.5, y + 1, 13, 5.5, 2, 1.3, color);
        c.DrawCircle(x - 3.5, y - 3.7, 0.8, color);
        c.DrawCircle(x - 3.5, y + 3.8, 0.8, color);
    }

    public static void Palette(Canvas c, double x, double y, uint color)
    {
        c.DrawRing(x, y, 6.5, 1.3, color);
        c.DrawCircle(x - 2.5, y - 1, 1, color);
        c.DrawCircle(x, y - 3, 1, color);
        c.DrawCircle(x + 3, y - 1.5, 1, color);
    }

    public static void Key(Canvas c, double x, double y, uint color)
    {
        c.DrawRing(x - 3.5, y + 3, 3, 1.3, color);
        c.DrawLine(x - 1.5, y + 1, x + 6, y - 6, 1.3, color);
        c.DrawLine(x + 3, y - 3, x + 5, y - 1, 1.3, color);
    }

    public static void Lock(Canvas c, double x, double y, uint color)
    {
        c.DrawRRectLine(x - 5.5, y - 1, 11, 7.5, 2, 1.3, color);
        c.DrawArc(x, y - 1.5, 3.2, 1.3, Math.PI, Math.PI * 2, color);
    }

    public static void Tray(Canvas c, double x, double y, uint color)
    {
        c.DrawRRectLine(x - 6.5, y - 5, 13, 11, 2, 1.3, color);
        c.DrawLine(x - 6.5, y + 0.5, x - 2.5, y + 0.5, 1.3, color);
        c.DrawLine(x - 2.5, y + 0.5, x - 1.5, y + 2.5, 1.3, color);
        c.DrawLine(x - 1.5, y + 2.5, x + 1.5, y + 2.5, 1.3, color);
        c.DrawLine(x + 1.5, y + 2.5, x + 2.5, y + 0.5, 1.3, color);
        c.DrawLine(x + 2.5, y + 0.5, x + 6.5, y + 0.5, 1.3, color);
    }

    public static void Wallet(Canvas c, double x, double y, uint color)
    {
        c.DrawRRectLine(x - 6.5, y - 4.5, 13, 10, 2, 1.3, color);
        c.DrawLine(x - 6.5, y - 1.5, x + 6.5, y - 1.5, 1.3, color);
        c.DrawCircle(x + 3, y + 2, 0.9, color);
    }

    public static void User(Canvas c, double x, double y, uint color)
    {
        c.DrawRing(x, y - 3, 2.8, 1.3, color);
        c.DrawArc(x, y + 6.5, 5.5, 1.3, Math.PI * 1.15, Math.PI * 1.85, color);
    }

    public static void Bell(Canvas c, double x, double y, uint color)
    {
        c.DrawArc(x, y - 0.5, 4.5, 1.3, Math.PI, Math.PI * 2, color);
        c.DrawLine(x - 4.5, y - 0.5, x - 5.5, y + 3.5, 1.3, color);
        c.DrawLine(x + 4.5, y - 0.5, x + 5.5, y + 3.5, 1.3, color);
        c.DrawLine(x - 5.5, y + 3.5, x + 5.5, y + 3.5, 1.3, color);
        c.DrawLine(x - 1.5, y + 6, x + 1.5, y + 6, 1.3, color);
    }

    public static void Copy(Canvas c, double x, double y, uint color)
    {
        c.DrawRRectLine(x - 3, y - 3, 8, 8, 1.5, 1.2, color);
        c.DrawLine(x - 5, y - 5, x - 5, y + 3, 1.2, color);
        c.DrawLine(x - 5, y - 5, x + 3, y - 5, 1.2, color);
    }
}
