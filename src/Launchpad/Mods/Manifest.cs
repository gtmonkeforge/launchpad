using System.Text.Json.Serialization;
using Launchpad.Infrastructure;

namespace Launchpad.Mods;

public sealed class Manifest
{
    public List<InstalledMod> Installed { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }

    public static string PathFor(string game) => Path.Combine(game, "forge.json");

    public static async Task<Manifest> ReadAsync(string game)
    {
        Manifest manifest = await Json.ReadFileAsync(PathFor(game), () => new Manifest());
        manifest.Installed ??= [];

        if (manifest.Installed.Any(mod => string.IsNullOrEmpty(mod.Guid) || mod.Files == null || mod.Folders == null))
            throw new UserFacingException("forge.json in the game folder is damaged. Repair or delete it, then try again.");

        return manifest;
    }

    public Task WriteAsync(string game) => Json.WriteFileAsync(PathFor(game), this);

    public InstalledMod? Find(string guid) => Installed.FirstOrDefault(mod => mod.Guid == guid);
}

public sealed class InstalledMod
{
    public string Guid { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public List<string> Files { get; set; } = [];
    public List<string> Folders { get; set; } = [];
    public ModLoader? Loader { get; set; }
    public string? Channel { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Injector { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Custom { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }

    [JsonIgnore]
    public ModLoader EffectiveLoader => Loader ?? (Files.Concat(Folders).Any(path =>
        System.Text.RegularExpressions.Regex.IsMatch(path, @"(^|[\\/])(Mods|Plugins)[\\/]", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        && !System.Text.RegularExpressions.Regex.IsMatch(path, @"(^|[\\/])BepInEx[\\/]", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        ? ModLoader.MelonLoader
        : ModLoader.Bepinex5);
}
