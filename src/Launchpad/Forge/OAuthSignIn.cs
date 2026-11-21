using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Launchpad.Forge;

// The browser half of signing in: Launchpad listens on a loopback port, opens MonkeForge's OAuth2 authorization page
// with a PKCE challenge, and waits for the browser to come back with a code (RFC 8252). ForgeClient.ExchangeAsync
// then trades the code for tokens.
public sealed partial class OAuthSignIn : IDisposable
{
    // identify comes with every token; account lets Launchpad use the API as the person (installs, purchases, devices).
    public const string Scope = "account";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancel = new(Lifetime);
    private readonly string _state;
    private readonly string _issuer;

    public OAuthSignIn(Uri forge, string clientId)
    {
        Verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        _state = Base64Url(RandomNumberGenerator.GetBytes(32));
        _issuer = forge.GetLeftPart(UriPartial.Authority);
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start(4);
        RedirectUri = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/callback";

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = RedirectUri,
            ["scope"] = Scope,
            ["state"] = _state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        };

        SignInUrl = new Uri(forge, "/oauth/authorize?" + string.Join('&', query.Select(pair => pair.Key + "=" + Uri.EscapeDataString(pair.Value))));
    }

    public string Verifier { get; }
    public string RedirectUri { get; }
    public Uri SignInUrl { get; }

    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [GeneratedRegex("^[A-Za-z0-9_-]{20,100}$")]
    private static partial Regex CodeFormat();

    [GeneratedRegex("^[a-z_]{1,64}$")]
    private static partial Regex ErrorFormat();

    // What came back on the loopback address: a code, the person's refusal or MonkeForge's error, or nothing usable.
    internal sealed record Callback(string? Code, string? Error, string? Description);

    public async Task<string> WaitForCodeAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cancel.Token);

        try
        {
            while (true)
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(linked.Token);
                Callback? result = await HandleAsync(client, linked.Token);

                if (result?.Code != null)
                    return result.Code;

                if (result?.Error == "access_denied")
                    throw new ForgeException(403, "You didn’t allow Launchpad to use your account, so you’re still signed out.");

                if (result?.Error != null)
                    throw new ForgeException(400, "MonkeForge couldn’t sign you in to Launchpad: " + (result.Description is { Length: > 0 } d ? d : result.Error));
            }
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new ForgeException(408, "Sign-in timed out. Start again when you’re ready.");
        }
    }

    internal Callback? Validate(string requestLine)
    {
        var parts = requestLine.Split(' ');

        if (parts.Length != 3 || parts[0] != "GET" || !parts[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
            return null;

        if (!Uri.TryCreate("http://127.0.0.1" + parts[1], UriKind.Absolute, out Uri? uri) || uri.AbsolutePath != "/callback")
            return null;

        var values = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(pair => (Key: Unescape(pair[0]), Value: pair.Length > 1 ? Unescape(pair[1]) : ""))
            .ToList();

        string? Single(string key)
        {
            List<string> found = values.Where(v => v.Key == key).Select(v => v.Value).ToList();
            return found.Count == 1 ? found[0] : null;
        }

        // Anything without our state is ignored: it didn't come from the sign-in we started.
        if (Single("state") is not { } state || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(_state), Encoding.ASCII.GetBytes(state)))
            return null;

        // RFC 9207: the authorization server names itself, so a response from somewhere else can't be slipped in.
        if (Single("iss") is { } issuer && !string.Equals(issuer.TrimEnd('/'), _issuer, StringComparison.OrdinalIgnoreCase))
            return null;

        if (Single("error") is { } error)
        {
            var description = Single("error_description");
            return new Callback(null, ErrorFormat().IsMatch(error) ? error : "server_error", description is { Length: <= 300 } ? description : null);
        }

        return Single("code") is { } code && CodeFormat().IsMatch(code) ? new Callback(code, null, null) : null;
    }

    private static string Unescape(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));

    private async Task<Callback?> HandleAsync(TcpClient client, CancellationToken ct)
    {
        client.ReceiveTimeout = 5000;
        await using NetworkStream stream = client.GetStream();
        var buffer = new byte[8192];
        var length = 0;

        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                while (length < buffer.Length && !Encoding.ASCII.GetString(buffer, 0, length).Contains("\r\n\r\n"))
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token);

                    if (read == 0)
                        break;

                    length += read;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return null;
            }
        }

        var request = Encoding.ASCII.GetString(buffer, 0, length);
        Callback? result = Validate(request.Split("\r\n", 2)[0]);

        var body = result switch
        {
            { Code: not null } => Page("You’re signed in", "Return to Launchpad. You can close this tab."),
            { Error: "access_denied" } => Page("Launchpad wasn’t allowed", "You’re still signed out. Return to Launchpad to try again."),
            { Error: not null } => Page("Sign-in didn’t work", "Return to Launchpad to see what happened and try again."),
            _ => Page("This link didn’t work", "Return to Launchpad and start signing in again."),
        };

        var ok = result?.Code != null;
        var head = $"HTTP/1.1 {(ok ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nReferrer-Policy: no-referrer\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(head + body), ct);
        return result;
    }

    private static string Page(string title, string text) => $$"""
        <!doctype html><html lang="en"><head><meta charset="utf-8"><title>{{title}} · Launchpad</title><meta name="color-scheme" content="dark">
        <style>body{margin:0;min-height:100vh;display:grid;place-items:center;background:#111214;color:#ececee;font:15px/1.55 ui-sans-serif,system-ui,"Segoe UI",Roboto,sans-serif}
        main{max-width:420px;padding:32px;border-radius:12px;background:#18191c;border:1px solid #2a2c31;text-align:center}h1{margin:0 0 8px;font-size:20px;font-weight:650}p{margin:0;color:#9b9ea6}</style>
        </head><body><main><h1>{{title}}</h1><p>{{text}}</p></main></body></html>
        """;

    public void Dispose()
    {
        _cancel.Cancel();
        _listener.Stop();
        _cancel.Dispose();
    }
}
