using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Launchpad.Mods;

namespace Launchpad.Infrastructure;

public sealed class Settings
{
    public ModLoader Loader { get; set; } = ModLoader.Bepinex5;
    public string Accent { get; set; } = "#dc5c3b";
    public Dictionary<string, string> Channels { get; set; } = [];
    public string? GamePath { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }

    public static async Task<Settings> LoadAsync()
    {
        Settings settings = await Json.ReadFileAsync(AppPaths.Settings, () => new Settings());

        if (string.IsNullOrWhiteSpace(settings.GamePath))
            settings.GamePath = await ReadMforgePathAsync();

        if (!System.Text.RegularExpressions.Regex.IsMatch(settings.Accent ?? "", "^#[0-9a-fA-F]{6}$"))
            settings.Accent = "#dc5c3b";

        return settings;
    }

    [JsonIgnore]
    public string FilePath { get; set; } = AppPaths.Settings;

    [JsonIgnore]
    public bool ShareWithCli { get; set; } = true;

    public async Task SaveAsync()
    {
        await Json.WriteFileAsync(FilePath, this);

        if (ShareWithCli)
            await WriteMforgePathAsync(GamePath);
    }

    private static async Task<string?> ReadMforgePathAsync()
    {
        JsonObject config = await Json.ReadFileAsync(AppPaths.MforgeConfig, () => new JsonObject());
        return config.Str("path") is { Length: > 0 } path ? path : null;
    }

    private static async Task WriteMforgePathAsync(string? path)
    {
        JsonObject config;

        try
        {
            config = await Json.ReadFileAsync(AppPaths.MforgeConfig, () => new JsonObject());
        }
        catch (UserFacingException)
        {
            return;
        }

        if (config.Str("path") == (path ?? ""))
            return;

        config["path"] = path ?? "";
        await Json.WriteFileAsync(AppPaths.MforgeConfig, config);
    }
}
