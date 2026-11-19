namespace Launchpad.Infrastructure;

public static class AppPaths
{
    public static string ConfigDir { get; } = OperatingSystem.IsWindows()
        ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        : Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

    private static readonly string? Override = Environment.GetEnvironmentVariable("LAUNCHPAD_HOME") is { Length: > 0 } home ? Path.GetFullPath(home) : null;

    public static string Root { get; } = Override ?? Path.Combine(ConfigDir, "launchpad");
    public static string Settings => Path.Combine(Root, "settings.json");
    public static string Devices => Path.Combine(Root, "devices.json");
    public static string Secrets => Path.Combine(Root, "secrets");

    public static string MforgeConfig => Override != null ? Path.Combine(Override, "mforge.json") : Path.Combine(ConfigDir, "mforge.json");
}
