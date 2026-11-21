namespace Launchpad.Infrastructure;

// Launchpad's own server (src/Launchpad.Server): it signs Launchpad in to monkeforge.org and hands out updates.
// Every request Launchpad makes to it is built from Root, so there's one address to change.
public static class LaunchpadServer
{
    public const string RootUrl = "https://launchpad.monkeforge.org";

    // Set LAUNCHPAD_APP_SERVER to point at a test server (https://, or http:// on this computer).
    public static Uri? Override { get; } = ReadOverride();

    public static Uri Root => Override ?? new Uri(RootUrl);

    public static Uri Resolve(string pathAndQuery) => new(Root, pathAndQuery);

    private static Uri? ReadOverride()
    {
        var value = Environment.GetEnvironmentVariable("LAUNCHPAD_APP_SERVER");

        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Uri.TryCreate(value.Trim().TrimEnd('/'), UriKind.Absolute, out Uri? uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            throw new UserFacingException("LAUNCHPAD_APP_SERVER must be an https:// URL, or http:// on this computer.");

        return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/");
    }
}
