using System.Text.Json.Serialization;

namespace Launchpad.Mods;

public enum ModLoader
{
    [JsonStringEnumMemberName("bepinex5")] Bepinex5,
    [JsonStringEnumMemberName("melonloader")] MelonLoader,
}

public static class Loaders
{
    public static string Id(this ModLoader loader) => loader == ModLoader.MelonLoader ? "melonloader" : "bepinex5";

    public static string DisplayName(this ModLoader loader) => loader == ModLoader.MelonLoader ? "MelonLoader" : "BepInEx 5";

    public static string RecordGuid(this ModLoader loader) => loader == ModLoader.MelonLoader ? "melonloader" : "bepinex";

    public static string Proxy(this ModLoader loader) => loader == ModLoader.MelonLoader ? "version" : "winhttp";

    public static IReadOnlyList<string> RuntimeFiles(this ModLoader loader) => loader == ModLoader.MelonLoader
        ? ["MelonLoader/net35/MelonLoader.dll", "MelonLoader/net6/MelonLoader.dll", "MelonLoader/MelonLoader.dll"]
        : ["BepInEx/core/BepInEx.dll"];

    public static bool RuntimeInstalled(this ModLoader loader, string game) => loader.RuntimeFiles().Any(file => File.Exists(Path.Combine(game, file)));

    public static IReadOnlyList<string> PluginFolders(this ModLoader loader) => loader == ModLoader.MelonLoader ? ["Mods", "Plugins"] : ["BepInEx/plugins"];

    public static ModLoader Other(this ModLoader loader) => loader == ModLoader.MelonLoader ? ModLoader.Bepinex5 : ModLoader.MelonLoader;

    public static ModLoader? Parse(string? value) => value switch
    {
        "bepinex5" => ModLoader.Bepinex5,
        "melonloader" => ModLoader.MelonLoader,
        _ => null,
    };
}
