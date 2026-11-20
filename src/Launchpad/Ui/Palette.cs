namespace Launchpad.Ui;

public static class Palette
{
    public const string DefaultAccent = "#dc5c3b";

    public static uint Accent { get; private set; } = 0xFFDC5C3B;
    public static uint AccentSoft { get; private set; } = 0x24DC5C3B;
    public static uint AccentLine { get; private set; } = 0x6BDC5C3B;
    public static uint AccentInk { get; private set; } = 0xFFFFFFFF;
    public static uint AccentHot { get; private set; } = 0xFFE46A49;
    public const uint Bg = 0xFF111214;
    public const uint Surface = 0xFF18191C;
    public const uint Surface2 = 0xFF1F2125;
    public const uint Hover = 0xFF24262B;
    public const uint Line = 0xFF2A2C31;
    public const uint LineStrong = 0xFF383B41;
    public const uint Text = 0xFFECECEE;
    public const uint Muted = 0xFF9B9EA6;
    public const uint Faint = 0xFF6D7079;
    public const uint Good = 0xFF5CC98F;
    public const uint Warn = 0xFFE5B85C;
    public const uint Bad = 0xFFF08585;
    public const uint Admin = 0xFFB9A4EC;
    public const uint Titlebar = 0xFF0C0D0F;
    public const uint CloseHover = 0xFFC42B1C;

    public const double Radius = 9;
    public const double RadiusLarge = 12;
    public const double TitlebarHeight = 52;

    public static bool SetAccent(string? hex)
    {
        if (hex == null || !System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9a-fA-F]{6}$"))
            return false;

        var rgb = Convert.ToUInt32(hex[1..], 16);
        var red = (rgb >> 16) & 255;
        var green = (rgb >> 8) & 255;
        var blue = rgb & 255;
        var luminance = Linear(red) * 0.2126 + Linear(green) * 0.7152 + Linear(blue) * 0.0722;
        Accent = 0xFF000000 | rgb;
        AccentSoft = 0x24000000 | rgb;
        AccentLine = 0x6B000000 | rgb;
        AccentInk = luminance > 0.4 ? 0xFF0C0D10 : 0xFFFFFFFF;
        AccentHot = 0xFF000000 | Lighten(red) << 16 | Lighten(green) << 8 | Lighten(blue);
        return true;
    }

    private static double Linear(uint channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static uint Lighten(uint channel) => Math.Min(255, channel + (255 - channel) / 14 + 6);
}
