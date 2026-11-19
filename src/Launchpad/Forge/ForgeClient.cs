using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Launchpad.Infrastructure;
using Launchpad.Security;

namespace Launchpad.Forge;

public sealed class ForgeException(int status, string message) : UserFacingException(message)
{
    public int Status { get; } = status;
}

public sealed class ForgeClient : IDisposable
{
    public const string OfficialUrl = "https://monkeforge.org";
    private const string SessionSecret = "session";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly HttpClient _pinned;
    private readonly ISecretStore _secrets;
    private readonly object _gate = new();
    private string _cookie = "";

    private volatile bool _persist;

    public ForgeClient(ISecretStore secrets, Uri? baseUri = null, HttpMessageHandler? handler = null)
    {
        _secrets = secrets;
        BaseUri = baseUri ?? ResolveBase();
        _http = new HttpClient(handler ?? NewHandler(false), handler == null) { Timeout = Timeout };
        _pinned = handler != null ? _http : new HttpClient(NewHandler(IsOfficial), true) { Timeout = Timeout };

        foreach (HttpClient client in new[] { _http, _pinned }.Distinct())
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MonkeForge-Launchpad", typeof(ForgeClient).Assembly.GetName().Version?.ToString(3) ?? "2.0.0"));
    }

    public Uri BaseUri { get; }
    public bool IsOfficial => BaseUri.Host.Equals("monkeforge.org", StringComparison.OrdinalIgnoreCase);
    public string Csrf { get; private set; } = "";
    public JsonObject? User { get; private set; }
    public string? UserId => User.Str("id");

    public string? RestoreError { get; private set; }

    public event Action? SessionChanged;

    private static Uri ResolveBase()
    {
        var custom = Environment.GetEnvironmentVariable("LAUNCHPAD_SERVER");

        if (string.IsNullOrWhiteSpace(custom))
            return new Uri(OfficialUrl);

        if (!Uri.TryCreate(custom.TrimEnd('/'), UriKind.Absolute, out Uri? uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            throw new UserFacingException("LAUNCHPAD_SERVER must be an https:// URL, or http:// on this computer.");

        return new Uri(uri.GetLeftPart(UriPartial.Authority));
    }

    private static SocketsHttpHandler NewHandler(bool pin)
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        if (pin)
            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, chain, errors) => Hardening.ValidatePinned(certificate as System.Security.Cryptography.X509Certificates.X509Certificate2, chain, errors);

        return handler;
    }

    public Uri Resolve(string path)
    {
        var uri = new Uri(BaseUri, path);

        if (uri.GetLeftPart(UriPartial.Authority) != BaseUri.GetLeftPart(UriPartial.Authority))
            throw new UserFacingException("Launchpad refused a request to an unexpected address.");

        return uri;
    }

    public async Task RestoreAsync(CancellationToken ct = default)
    {
        var saved = _secrets.Get(SessionSecret);
        var cookie = saved == null ? "" : Encoding.UTF8.GetString(saved);

        if (!cookie.StartsWith("session=", StringComparison.Ordinal) || cookie.Contains('\r') || cookie.Contains('\n'))
        {
            if (saved != null)
                _secrets.Delete(SessionSecret);

            return;
        }

        lock (_gate)
            _cookie = cookie;

        _persist = true;

        try
        {
            await RefreshSessionAsync(ct);
            RestoreError = null;
        }
        catch (ForgeException ex) when (ex.Status is 0 or >= 500)
        {
            RestoreError = ex.Message;
        }
    }

    public async Task RefreshSessionAsync(CancellationToken ct = default)
    {
        JsonNode session = await GetJsonAsync("/api/v1/session", ct);

        if (session["user"] is not JsonObject user || session.Str("csrf_token") is not { Length: > 0 } csrf)
        {
            ClearSession();
            return;
        }

        User = (JsonObject)user.DeepClone();
        Csrf = csrf;
        SessionChanged?.Invoke();
    }

    public void ClearSession()
    {
        bool had;

        lock (_gate)
        {
            had = _cookie.Length > 0 || User != null;
            _cookie = "";
        }

        _persist = false;

        User = null;
        Csrf = "";
        _secrets.Delete(SessionSecret);

        if (had)
            SessionChanged?.Invoke();
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content = null, bool pinned = false, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(method, Resolve(path)) { Content = content };
        string cookie;

        lock (_gate)
            cookie = _cookie;

        if (cookie.Length > 0)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);

        if (method != HttpMethod.Get && method != HttpMethod.Head && Csrf.Length > 0)
            request.Headers.TryAddWithoutValidation("X-CSRF-Token", Csrf);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        HttpResponseMessage response;

        try
        {
            response = await (pinned ? _pinned : _http).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Security.Authentication.AuthenticationException)
        {
            throw new ForgeException(0, "MonkeForge's security certificate couldn't be verified. If you use a proxy or antivirus that inspects HTTPS, allow monkeforge.org.");
        }
        catch (HttpRequestException)
        {
            throw new ForgeException(0, "MonkeForge couldn't be reached. Check your internet connection.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ForgeException(0, "MonkeForge took too long to respond. Try again.");
        }

        CaptureCookie(response);

        if (response.IsSuccessStatusCode)
            return response;

        using (response)
        {
            var message = await ErrorMessageAsync(response, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized && User != null && path != "/api/v1/session" && !path.StartsWith("/api/v1/injector/", StringComparison.Ordinal))
            {
                ClearSession();
            }

            throw new ForgeException((int)response.StatusCode, message);
        }
    }

    public async Task<HttpResponseMessage> ForwardAsync(HttpMethod method, string pathAndQuery, HttpContent? content, string? accept, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, Resolve(pathAndQuery)) { Content = content };
        string cookie;

        lock (_gate)
            cookie = _cookie;

        if (cookie.Length > 0)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);

        if (method != HttpMethod.Get && method != HttpMethod.Head && Csrf.Length > 0)
            request.Headers.TryAddWithoutValidation("X-CSRF-Token", Csrf);

        if (!string.IsNullOrEmpty(accept))
            request.Headers.TryAddWithoutValidation("Accept", accept);

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException)
        {
            throw new ForgeException(0, "MonkeForge couldn't be reached. Check your internet connection.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ForgeException(0, "MonkeForge took too long to respond. Try again.");
        }

        CaptureCookie(response);

        if (response.StatusCode == HttpStatusCode.Unauthorized && User != null && !pathAndQuery.StartsWith("/api/v1/session", StringComparison.Ordinal))
            ClearSession();

        return response;
    }

    private static async Task<string> ErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            JsonNode? body = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));

            if (body.Str("error") is { Length: > 0 } error)
                return error;
        }
        catch (JsonException)
        {
        }

        return response.StatusCode switch
        {
            HttpStatusCode.TooManyRequests => "Too many requests. Wait a minute and try again.",
            HttpStatusCode.NotFound => "MonkeForge couldn't find that.",
            HttpStatusCode.Forbidden => "MonkeForge refused that request.",
            >= HttpStatusCode.InternalServerError => "MonkeForge had a problem. Try again in a moment.",
            _ => $"MonkeForge returned {(int)response.StatusCode}.",
        };
    }

    private void CaptureCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values))
            return;

        foreach (var header in values)
        {
            var pair = header.Split(';', 2)[0].Trim();

            if (!pair.StartsWith("session=", StringComparison.Ordinal))
                continue;

            var deleted = pair == "session=" || header.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase) || header.Contains("max-age=0", StringComparison.OrdinalIgnoreCase);

            lock (_gate)
            {
                if (deleted)
                {
                    _cookie = "";
                }
                else if (pair != _cookie)
                {
                    _cookie = pair;
                }
                else
                {
                    return;
                }
            }

            if (deleted)
            {
                _persist = false;
                _secrets.Delete(SessionSecret);
            }
            else if (_persist || response.RequestMessage?.RequestUri?.AbsolutePath == "/api/v1/auth/native/exchange")
            {
                _persist = true;
                _secrets.Set(SessionSecret, Encoding.UTF8.GetBytes(pair));
            }
        }
    }

    public async Task<JsonNode> GetJsonAsync(string path, CancellationToken ct = default)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, path, null, false, ct);
        return await ReadJsonAsync(response, ct);
    }

    public async Task<JsonNode> SendJsonAsync(HttpMethod method, string path, object? body, bool pinned = false, CancellationToken ct = default)
    {
        HttpContent? content = body == null ? null : JsonContent.Create(body, body.GetType(), options: Json.Options);
        using HttpResponseMessage response = await SendAsync(method, path, content, pinned, ct);
        return await ReadJsonAsync(response, ct);
    }

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);

        try
        {
            return JsonNode.Parse(text.Length == 0 ? "{}" : text) ?? new JsonObject();
        }
        catch (JsonException)
        {
            throw new ForgeException((int)response.StatusCode, "MonkeForge sent a response Launchpad couldn't read.");
        }
    }

    public async Task ExchangeAsync(string code, string verifier, string redirectUri, CancellationToken ct)
    {
        lock (_gate)
            _cookie = "";

        JsonNode result = await SendJsonAsync(HttpMethod.Post, "/api/v1/auth/native/exchange", new { code, code_verifier = verifier, redirect_uri = redirectUri }, false, ct);

        lock (_gate)
        {
            if (_cookie.Length == 0)
                throw new ForgeException(500, "MonkeForge didn't return a session. Try signing in again.");
        }

        if (result["user"] is not JsonObject user || result.Str("csrf_token") is not { Length: > 0 } csrf)
            throw new ForgeException(500, "MonkeForge's sign-in response was incomplete. Try again.");

        User = (JsonObject)user.DeepClone();
        Csrf = csrf;
        RestoreError = null;
        SessionChanged?.Invoke();
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try
        {
            if (User != null)
                await SendJsonAsync(HttpMethod.Post, "/api/v1/logout", new { }, false, ct);
        }
        catch (ForgeException)
        {
        }
        finally
        {
            ClearSession();
        }
    }

    public void Dispose()
    {
        _http.Dispose();

        if (!ReferenceEquals(_pinned, _http))
            _pinned.Dispose();
    }
}
