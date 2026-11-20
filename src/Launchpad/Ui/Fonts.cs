using GoomeSharp;

namespace Launchpad.Ui;

public static class Fonts
{
    public static Font Main { get; } = Font.System() ?? Font.BuiltIn;
    public static Font Mono { get; } = Font.System("monospace") ?? Main;
}
