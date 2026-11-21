namespace Launchpad.Server;

// Settings come from environment variables, or a .env file next to the server (KEY=value per line).
// Real environment variables win over the file.
public sealed class ServerOptions
{
    public const string DefaultClientId = "5fadc971d6867d50fcbd0dd6";

    public required Uri ForgeUrl { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string HomepageUrl { get; init; }
    public required string DataDir { get; init; }
    public required string PublishToken { get; init; }
    public required string ReleasePublicKey { get; init; }
    public required bool TrustCloudflare { get; init; }
    public required long MaxUploadBytes { get; init; }

    public string ReleasesDir => Path.Combine(DataDir, "releases");
    public bool TokensConfigured => ClientSecret.Length > 0;
    public bool PublishingEnabled => PublishToken.Length >= 32;

    public static ServerOptions Load()
    {
        foreach (var file in new[] { Path.Combine(Directory.GetCurrentDirectory(), ".env"), Path.Combine(AppContext.BaseDirectory, ".env") }.Distinct())
            LoadDotEnv(file);

        static string Get(string name, string fallback = "") => Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } value ? value : fallback;

        var forge = Get("FORGE_URL", "https://monkeforge.org").TrimEnd('/');

        if (!Uri.TryCreate(forge, UriKind.Absolute, out Uri? forgeUri) || (forgeUri.Scheme != "https" && !forgeUri.IsLoopback))
            throw new InvalidOperationException("FORGE_URL must be an https:// URL.");

        var dataDir = Path.GetFullPath(Get("DATA_DIR", Path.Combine(AppContext.BaseDirectory, "data")));

        return new ServerOptions
        {
            ForgeUrl = new Uri(forgeUri.GetLeftPart(UriPartial.Authority)),
            ClientId = Get("LAUNCHPAD_CLIENT_ID", DefaultClientId),
            ClientSecret = Get("LAUNCHPAD_CLIENT_SECRET"),
            HomepageUrl = Get("HOMEPAGE_URL", "https://monkeforge.org/software/launchpad"),
            DataDir = dataDir,
            PublishToken = Get("PUBLISH_TOKEN"),
            ReleasePublicKey = Get("RELEASE_PUBLIC_KEY"),
            TrustCloudflare = Get("TRUST_CLOUDFLARE", "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            MaxUploadBytes = long.TryParse(Get("MAX_UPLOAD_MB"), out var mb) && mb > 0 ? mb * 1024 * 1024 : 1024L * 1024 * 1024,
        };
    }

    private static void LoadDotEnv(string path)
    {
        if (!File.Exists(path))
            return;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var equals = line.IndexOf('=');

            if (equals <= 0)
                continue;

            var key = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();

            if (value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\''))
                value = value[1..^1];

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
