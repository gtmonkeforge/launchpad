using GoomeSharp;

namespace Launchpad.Ui;

public static class LaunchpadTheme
{
    public static void Apply(Theme theme)
    {
        theme.Bg = Palette.Bg;
        theme.Panel = Palette.Surface;
        theme.Border = Palette.Line;
        theme.Text = Palette.Text;
        theme.TextDim = Palette.Muted;
        theme.Accent = Palette.Accent;
        theme.AccentText = Palette.AccentInk;
        theme.Hover = Palette.Hover;
        theme.Active = Palette.Surface2;
        theme.InputBg = Palette.Bg;
        theme.Focus = Palette.Accent;
        theme.Radius = Palette.Radius;
        theme.FontSize = 15;
        theme.Link = Palette.Accent;
        theme.Danger = Palette.Bad;
    }
}
