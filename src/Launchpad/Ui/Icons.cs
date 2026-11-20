using GoomeSharp;

namespace Launchpad.Ui;

public static class Icons
{
    public static void Back(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x + 2, y - 6, x - 4, y, 1.7, color);
        c.DrawLine(x - 4, y, x + 2, y + 6, 1.7, color);
    }

    public static void Forward(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x - 2, y - 6, x + 4, y, 1.7, color);
        c.DrawLine(x + 4, y, x - 2, y + 6, 1.7, color);
    }

    public static void Reload(Canvas c, double x, double y, uint color)
    {
        c.DrawArc(x, y, 6.5, 1.7, 0.6, 5.6, color);
        c.DrawLine(x + 6, y - 6, x + 6.5, y - 1.5, 1.7, color);
        c.DrawLine(x + 6, y - 6, x + 1.5, y - 5.5, 1.7, color);
    }

    public static void Search(Canvas c, double x, double y, uint color)
    {
        c.DrawRing(x - 1, y - 1, 5.5, 1.5, color);
        c.DrawLine(x + 3, y + 3, x + 7, y + 7, 1.6, color);
    }

    public static void Bell(Canvas c, double x, double y, uint color)
    {
        c.DrawArc(x, y - 1, 5.5, 1.5, 3.14, 6.28, color);
        c.DrawLine(x - 5.5, y - 1, x - 6.5, y + 4, 1.5, color);
        c.DrawLine(x + 5.5, y - 1, x + 6.5, y + 4, 1.5, color);
        c.DrawLine(x - 7, y + 4.5, x + 7, y + 4.5, 1.5, color);
        c.DrawLine(x - 1.5, y + 8, x + 1.5, y + 8, 1.5, color);
    }

    public static void Minimize(Canvas c, double x, double y, uint color) => c.DrawLine(x - 6, y, x + 6, y, 1.5, color);

    public static void Maximize(Canvas c, double x, double y, uint color) => c.DrawRRectLine(x - 5.5, y - 5.5, 11, 11, 1.5, 1.5, color);

    public static void Restore(Canvas c, double x, double y, uint color)
    {
        c.DrawRRectLine(x - 6, y - 3, 9, 9, 1.5, 1.5, color);
        c.DrawLine(x - 3, y - 6, x + 5, y - 6, 1.5, color);
        c.DrawLine(x + 5, y - 6, x + 5, y + 3, 1.5, color);
    }

    public static void Close(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x - 5, y - 5, x + 5, y + 5, 1.5, color);
        c.DrawLine(x + 5, y - 5, x - 5, y + 5, 1.5, color);
    }

    public static void Gear(Canvas c, double x, double y, uint color)
    {
        c.DrawRing(x, y, 5.5, 1.5, color);
        c.DrawRing(x, y, 2, 1.3, color);

        for (var i = 0; i < 8; i++)
        {
            var angle = i * Math.PI / 4;
            c.DrawLine(x + Math.Cos(angle) * 6.2, y + Math.Sin(angle) * 6.2, x + Math.Cos(angle) * 8.2, y + Math.Sin(angle) * 8.2, 1.7, color);
        }
    }

    public static void User(Canvas c, double x, double y, uint color)
    {
        c.DrawRing(x, y - 3, 3, 1.3, color);
        c.DrawArc(x, y + 7, 6, 1.3, 3.5, 5.9, color);
    }

    public static void Inbox(Canvas c, double x, double y, uint color)
    {
        c.DrawRRectLine(x - 7, y - 5, 14, 11, 2, 1.3, color);
        c.DrawLine(x - 7, y + 1, x - 3, y + 1, 1.3, color);
        c.DrawLine(x - 3, y + 1, x - 2, y + 3, 1.3, color);
        c.DrawLine(x - 2, y + 3, x + 2, y + 3, 1.3, color);
        c.DrawLine(x + 2, y + 3, x + 3, y + 1, 1.3, color);
        c.DrawLine(x + 3, y + 1, x + 7, y + 1, 1.3, color);
    }

    public static void Rocket(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x - 6, y + 6, x + 1, y - 1, 1.4, color);
        c.DrawRing(x + 3.5, y - 3.5, 3.5, 1.3, color);
        c.DrawLine(x - 5, y + 2, x - 2, y + 5, 1.3, color);
    }

    public static void Trash(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x - 6, y - 4, x + 6, y - 4, 1.3, color);
        c.DrawLine(x - 2, y - 4, x - 2, y - 6, 1.3, color);
        c.DrawLine(x + 2, y - 4, x + 2, y - 6, 1.3, color);
        c.DrawLine(x - 2, y - 6, x + 2, y - 6, 1.3, color);
        c.DrawLine(x - 4.5, y - 4, x - 4, y + 6, 1.3, color);
        c.DrawLine(x + 4.5, y - 4, x + 4, y + 6, 1.3, color);
        c.DrawLine(x - 4, y + 6, x + 4, y + 6, 1.3, color);
    }

    public static void SmallClose(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x - 3.5, y - 3.5, x + 3.5, y + 3.5, 1.4, color);
        c.DrawLine(x + 3.5, y - 3.5, x - 3.5, y + 3.5, 1.4, color);
    }

    public static void Plus(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x - 5, y, x + 5, y, 1.7, color);
        c.DrawLine(x, y - 5, x, y + 5, 1.7, color);
    }

    public static void Download(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x, y - 5, x, y + 2, 1.2, color);
        c.DrawLine(x - 3, y - 1, x, y + 2, 1.2, color);
        c.DrawLine(x + 3, y - 1, x, y + 2, 1.2, color);
        c.DrawLine(x - 5, y + 5, x + 5, y + 5, 1.2, color);
    }

    public static void Clock(Canvas c, double x, double y, uint color)
    {
        c.DrawRing(x, y, 5.5, 1.2, color);
        c.DrawLine(x, y, x, y - 3, 1.2, color);
        c.DrawLine(x, y, x + 2.5, y + 1.5, 1.2, color);
    }

    public static void Check(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x - 4, y, x - 1, y + 3, 1.5, color);
        c.DrawLine(x - 1, y + 3, x + 4, y - 3, 1.5, color);
    }

    public static void Shield(Canvas c, double x, double y, uint color)
    {
        c.DrawLine(x - 4, y - 4, x + 4, y - 4, 1.3, color);
        c.DrawLine(x - 4, y - 4, x - 4, y + 1, 1.3, color);
        c.DrawLine(x + 4, y - 4, x + 4, y + 1, 1.3, color);
        c.DrawLine(x - 4, y + 1, x, y + 5, 1.3, color);
        c.DrawLine(x + 4, y + 1, x, y + 5, 1.3, color);
    }

    public static void Category(Canvas c, string name, double x, double y, uint color)
    {
        switch (name)
        {
            case "":
                foreach (var (dx, dy) in new[] { (-4.5, -4.5), (0.5, -4.5), (-4.5, 0.5), (0.5, 0.5) })
                    c.DrawRRectLine(x + dx, y + dy, 4, 4, 1, 1.2, color);
                break;
            case "Gameplay":
                c.DrawRRectLine(x - 7, y - 4, 14, 8, 3.5, 1.3, color);
                c.DrawLine(x - 3.5, y - 1.5, x - 3.5, y + 1.5, 1.2, color);
                c.DrawLine(x - 5, y, x - 2, y, 1.2, color);
                c.DrawCircle(x + 3.5, y, 1, color);
                break;
            case "Cosmetic":
                c.DrawRing(x, y, 6.5, 1.3, color);
                c.DrawCircle(x - 2.5, y - 2.5, 1, color);
                c.DrawCircle(x + 2.5, y - 2.5, 1, color);
                c.DrawCircle(x - 3, y + 2.5, 1, color);
                break;
            case "Utilities":
                c.DrawLine(x - 6, y + 6, x + 2, y - 2, 1.4, color);
                c.DrawLine(x + 6, y - 6, x + 1, y - 1, 1.4, color);
                c.DrawLine(x + 6, y - 2, x + 2, y - 6, 1.4, color);
                break;
            case "Accessibility":
                c.DrawCircle(x, y - 5, 1.5, color);
                c.DrawLine(x - 5, y - 2, x + 5, y - 2, 1.3, color);
                c.DrawLine(x, y - 2, x, y + 2, 1.3, color);
                c.DrawLine(x, y + 2, x - 3, y + 6, 1.3, color);
                c.DrawLine(x, y + 2, x + 3, y + 6, 1.3, color);
                break;
            default:
                c.DrawRRectLine(x - 6, y - 5, 12, 11, 1.5, 1.3, color);
                c.DrawLine(x - 6, y - 1, x + 6, y - 1, 1.2, color);
                break;
        }
    }
}
