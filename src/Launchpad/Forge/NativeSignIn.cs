using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Launchpad.Forge;

public sealed partial class NativeSignIn : IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancel = new(Lifetime);
    private readonly string _state;

    public NativeSignIn(Uri forge)
    {
        Verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        _state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start(4);
        RedirectUri = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/callback";

        var query = new Dictionary<string, string>
        {
            ["redirect_uri"] = RedirectUri,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = _state,
        };

        SignInUrl = new Uri(forge, "/login?" + string.Join('&', query.Select(pair => pair.Key + "=" + Uri.EscapeDataString(pair.Value))));
    }

    public string Verifier { get; }
    public string RedirectUri { get; }
    public Uri SignInUrl { get; }

    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex Token43();

    public async Task<string> WaitForCodeAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cancel.Token);

        try
        {
            while (true)
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(linked.Token);
                var code = await HandleAsync(client, linked.Token);

                if (code != null)
                    return code;
            }
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new ForgeException(408, "Sign-in timed out. Start again when you're ready.");
        }
    }

    internal string? Validate(string requestLine)
    {
        var parts = requestLine.Split(' ');

        if (parts.Length != 3 || parts[0] != "GET" || !parts[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
            return null;

        if (!Uri.TryCreate("http://127.0.0.1" + parts[1], UriKind.Absolute, out Uri? uri) || uri.AbsolutePath != "/callback")
            return null;

        var values = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(pair => (Key: Uri.UnescapeDataString(pair[0]), Value: pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : ""))
            .ToList();

        List<string> states = values.Where(v => v.Key == "state").Select(v => v.Value).ToList();
        List<string> codes = values.Where(v => v.Key == "code").Select(v => v.Value).ToList();

        if (states.Count != 1 || codes.Count != 1 || !Token43().IsMatch(codes[0]))
            return null;

        var expected = Encoding.ASCII.GetBytes(_state);
        var received = Encoding.ASCII.GetBytes(states[0]);

        return CryptographicOperations.FixedTimeEquals(expected, received) ? codes[0] : null;
    }

    private async Task<string?> HandleAsync(TcpClient client, CancellationToken ct)
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
        var code = Validate(request.Split("\r\n", 2)[0]);

        var body = code != null
            ? Page("You're signed in", "Return to Launchpad. You can close this tab.")
            : Page("This link didn't work", "Return to Launchpad and start signing in again.");

        var head = $"HTTP/1.1 {(code != null ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nReferrer-Policy: no-referrer\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(head + body), ct);
        return code;
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
