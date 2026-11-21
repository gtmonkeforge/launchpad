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

// Talks to MonkeForge as the signed-in person. Launchpad is an OAuth2 app: it holds an access token (an hour) and a
// refresh token (60 days, replaced on every use) and sends the access token as a bearer token. Bearer requests don't
// need MonkeForge's CSRF token or cookies.
//
// On monkeforge.org the token requests go through Launchpad's own server (LaunchpadServer.Root), which adds the
// app's client secret. Against a self-hosted server (LAUNCHPAD_SERVER), Launchpad acts as a public client and talks to
// that server's token endpoint directly; set LAUNCHPAD_CLIENT_ID to an app registered there.
public sealed class ForgeClient : IDisposable
{
    public const string OfficialUrl = "https://monkeforge.org";
    public const string OfficialClientId = "5fadc971d6867d50fcbd0dd6";

    private const string TokenSecret = "oauth";
    private const string LegacySessionSecret = "session";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(2);

    private readonly HttpClient _http;
    private readonly HttpClient _pinned;
    private readonly ISecretStore _secrets;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _persistGate = new();
    private volatile Tokens? _tokens;

    private sealed record Tokens(string Access, string Refresh, DateTimeOffset Expires, string Scope);

    private sealed class TokenRejectedException(string message) : Exception(message);

    public ForgeClient(ISecretStore secrets, Uri? baseUri = null, HttpMessageHandler? handler = null)
    {
        _secrets = secrets;
        BaseUri = baseUri ?? ResolveBase();
        _http = new HttpClient(handler ?? NewHandler(false), handler == null) { Timeout = Timeout };
        _pinned = handler != null ? _http : new HttpClient(NewHandler(IsOfficial), true) { Timeout = Timeout };

        foreach (HttpClient client in new[] { _http, _pinned }.Distinct())
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MonkeForge-Launchpad", typeof(ForgeClient).Assembly.GetName().Version?.ToString(3) ?? "2.0.0"));

        (ClientId, TokenEndpoint, RevokeEndpoint) = ResolveAuth();
    }

    public Uri BaseUri { get; }
    public bool IsOfficial => BaseUri.Host.Equals("monkeforge.org", StringComparison.OrdinalIgnoreCase);
    public string ClientId { get; }
    public Uri TokenEndpoint { get; }
    public Uri RevokeEndpoint { get; }
    public JsonObject? User { get; private set; }
    public string? UserId => User.Str("id");
    public bool SignedIn => _tokens != null;

    public string? RestoreError { get; private set; }

    public event Action? SessionChanged;

    private static Uri ResolveBase()
    {
        var custom = Environment.GetEnvironmentVariable("LAUNCHPAD_SERVER");

        if (string.IsNullOrWhiteSpace(custom))
            return new Uri(OfficialUrl);

        return new Uri(CheckedUrl(custom, "LAUNCHPAD_SERVER").GetLeftPart(UriPartial.Authority));
    }

    private static Uri CheckedUrl(string value, string name)
    {
        if (!Uri.TryCreate(value.Trim().TrimEnd('/'), UriKind.Absolute, out Uri? uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            throw new UserFacingException($"{name} must be an https:// URL, or http:// on this computer.");

        return uri;
    }

    private (string ClientId, Uri Token, Uri Revoke) ResolveAuth()
    {
        var clientOverride = Environment.GetEnvironmentVariable("LAUNCHPAD_CLIENT_ID")?.Trim();

        // monkeforge.org: through the Launchpad server, which holds the client secret.
        if (IsOfficial)
            return (string.IsNullOrEmpty(clientOverride) ? OfficialClientId : clientOverride, LaunchpadServer.Resolve("/api/v1/oauth/token"), LaunchpadServer.Resolve("/api/v1/oauth/revoke"));

        // A self-hosted server: straight to it as a public client, unless a test Launchpad server is set.
        Uri authServer = LaunchpadServer.Override ?? BaseUri;
        return (clientOverride ?? "", new Uri(authServer, "/api/v1/oauth/token"), new Uri(authServer, "/api/v1/oauth/revoke"));
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

    // ---- tokens -----------------------------------------------------------------------------------------------------

    public OAuthSignIn StartSignIn()
    {
        if (ClientId.Length == 0)
            throw new UserFacingException("Signing in to this server needs an OAuth app. Register a public client there and set LAUNCHPAD_CLIENT_ID.");

        return new OAuthSignIn(BaseUri, ClientId);
    }

    public async Task RestoreAsync(CancellationToken ct = default)
    {
        // Launchpad 2.0 kept a website session cookie. MonkeForge no longer issues those to apps, so it's dropped.
        Forget(LegacySessionSecret);

        Tokens? saved = ReadSaved();

        if (saved == null)
            return;

        _tokens = saved;

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

    private Tokens? ReadSaved()
    {
        byte[]? bytes;

        try
        {
            bytes = _secrets.Get(TokenSecret);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or UserFacingException)
        {
            return null;
        }

        if (bytes == null)
            return null;

        try
        {
            JsonNode? node = JsonNode.Parse(bytes);

            if (node.Str("access_token") is { Length: > 0 } access && node.Str("refresh_token") is { Length: > 0 } refresh && Clean(access) && Clean(refresh))
                return new Tokens(access, refresh, DateTimeOffset.FromUnixTimeSeconds(node.Long("expires_at")), node.Str("scope") ?? "");
        }
        catch (JsonException)
        {
        }

        Forget(TokenSecret);
        return null;
    }

    private static bool Clean(string token) => token.Length <= 512 && token.All(c => c > ' ' && c < 127);

    private async Task<Tokens> RequestTokensAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        form["client_id"] = ClientId;
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw new ForgeException(0, "Launchpad’s sign-in server couldn’t be reached. Check your internet connection.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ForgeException(0, "Launchpad’s sign-in server took too long to respond. Try again.");
        }

        using (response)
        {
            JsonNode? body = null;

            try
            {
                body = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
            }
            catch (JsonException)
            {
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = body.Str("error");
                var description = body.Str("error_description") is { Length: > 0 } d ? d : $"The sign-in server returned {(int)response.StatusCode}.";

                if (error == "invalid_grant")
                    throw new TokenRejectedException(description);

                throw new ForgeException(response.StatusCode == HttpStatusCode.TooManyRequests ? 429 : (int)response.StatusCode, description);
            }

            if (body.Str("access_token") is not { Length: > 0 } access || body.Str("refresh_token") is not { Length: > 0 } refresh || !Clean(access) || !Clean(refresh)
                || !string.Equals(body.Str("token_type"), "Bearer", StringComparison.OrdinalIgnoreCase))
                throw new ForgeException(502, "The sign-in server sent a response Launchpad couldn’t read.");

            var lifetime = Math.Clamp(body.Long("expires_in") is > 0 and var seconds ? seconds : 3600, 60, 24 * 3600);
            return new Tokens(access, refresh, DateTimeOffset.UtcNow.AddSeconds(lifetime), body.Str("scope") ?? "");
        }
    }

    // Returns a usable access token, refreshing it when it's about to expire or after the server rejected it.
    // Refreshes are serialized: refresh tokens rotate, so two at once would sign Launchpad out.
    private async Task<string?> AccessTokenAsync(string? rejected, CancellationToken ct)
    {
        bool Usable(Tokens t) => rejected == null ? t.Expires - DateTimeOffset.UtcNow > RefreshMargin : t.Access != rejected;

        Tokens? current = _tokens;

        if (current == null)
            return null;

        if (Usable(current))
            return current.Access;

        await _refreshGate.WaitAsync(ct);

        try
        {
            current = _tokens;

            if (current == null)
                return null;

            if (Usable(current))
                return current.Access;

            Tokens fresh;

            try
            {
                // Not cancellable: once the server has rotated the refresh token, the new one has to be kept.
                fresh = await RequestTokensAsync(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["refresh_token"] = current.Refresh }, CancellationToken.None);
            }
            catch (TokenRejectedException)
            {
                // Revoked, expired, or signed out everywhere: sign in again.
                if (ReferenceEquals(_tokens, current))
                    ClearSession();

                return null;
            }

            if (!ReferenceEquals(_tokens, current))
                return _tokens?.Access;

            _tokens = fresh;
            Persist(fresh);
            return fresh.Access;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public async Task RefreshSessionAsync(CancellationToken ct = default)
    {
        JsonNode session = await GetJsonAsync("/api/v1/session", ct);

        if (session["user"] is not JsonObject user)
        {
            ClearSession();
            return;
        }

        User = (JsonObject)user.DeepClone();
        SessionChanged?.Invoke();
    }

    public void ClearSession()
    {
        var had = _tokens != null || User != null;
        _tokens = null;
        User = null;
        Persist(null);

        if (had)
            SessionChanged?.Invoke();
    }

    public async Task ExchangeAsync(string code, string verifier, string redirectUri, CancellationToken ct)
    {
        Tokens tokens;

        try
        {
            tokens = await RequestTokensAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = verifier,
            }, CancellationToken.None);
        }
        catch (TokenRejectedException ex)
        {
            throw new ForgeException(400, ex.Message);
        }

        if (!tokens.Scope.Split(' ').Contains(OAuthSignIn.Scope))
        {
            await RevokeAsync(tokens.Refresh);
            throw new ForgeException(403, "MonkeForge didn’t give Launchpad access to your account, so it can’t install mods for you. Try signing in again.");
        }

        _tokens = tokens;
        Persist(tokens);

        try
        {
            await RefreshSessionAsync(ct);
        }
        catch
        {
            ClearSession();
            throw;
        }

        if (User == null)
            throw new ForgeException(500, "MonkeForge didn’t return your account. Try signing in again.");

        RestoreError = null;
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        Tokens? tokens = _tokens;
        ClearSession();

        if (tokens != null)
            await RevokeAsync(tokens.Refresh);
    }

    // Revoking the refresh token ends this sign-in on MonkeForge too. Best-effort: signing out locally always works.
    private async Task RevokeAsync(string refresh)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refresh, ["client_id"] = ClientId });
            using HttpResponseMessage _ = await _http.PostAsync(RevokeEndpoint, content, timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
        }
    }

    private void Persist(Tokens? tokens)
    {
        lock (_persistGate)
        {
            try
            {
                if (tokens == null)
                {
                    _secrets.Delete(TokenSecret);
                }
                else if (ReferenceEquals(_tokens, tokens))
                {
                    var json = new JsonObject
                    {
                        ["access_token"] = tokens.Access,
                        ["refresh_token"] = tokens.Refresh,
                        ["expires_at"] = tokens.Expires.ToUnixTimeSeconds(),
                        ["scope"] = tokens.Scope,
                    };

                    _secrets.Set(TokenSecret, Encoding.UTF8.GetBytes(json.ToJsonString()));
                }
            }
            // Saving is best-effort: a failed save must never fail the request that refreshed the token.
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or UserFacingException or TimeoutException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        }
    }

    private void Forget(string name)
    {
        lock (_persistGate)
        {
            try
            {
                if (_secrets.Get(name) != null)
                    _secrets.Delete(name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or UserFacingException or TimeoutException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        }
    }

    // ---- API requests -----------------------------------------------------------------------------------------------

    private HttpRequestMessage NewRequest(HttpMethod method, string pathAndQuery, HttpContent? content, string? access, string? accept)
    {
        var request = new HttpRequestMessage(method, Resolve(pathAndQuery)) { Content = content };

        if (access != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);

        if (string.IsNullOrEmpty(accept))
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        else
            request.Headers.TryAddWithoutValidation("Accept", accept);

        return request;
    }

    private async Task<HttpResponseMessage> TransmitAsync(HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
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
    }

    // Sends with the current access token. A 401 means the token was revoked or expired early: refresh it, and retry
    // once if the request has no body to resend. If the refresh is refused, the person is signed out.
    private async Task<HttpResponseMessage> AuthorizedAsync(HttpClient client, HttpMethod method, string pathAndQuery, HttpContent? content, string? accept, CancellationToken ct)
    {
        var access = await AccessTokenAsync(null, ct);
        HttpResponseMessage response;

        using (HttpRequestMessage request = NewRequest(method, pathAndQuery, content, access, accept))
            response = await TransmitAsync(client, request, ct);

        if (response.StatusCode != HttpStatusCode.Unauthorized || access == null)
            return response;

        var fresh = await AccessTokenAsync(access, ct);

        if (fresh == null || content != null)
            return response;

        response.Dispose();

        using (HttpRequestMessage retry = NewRequest(method, pathAndQuery, null, fresh, accept))
            return await TransmitAsync(client, retry, ct);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content = null, bool pinned = false, CancellationToken ct = default)
    {
        HttpResponseMessage response = await AuthorizedAsync(pinned ? _pinned : _http, method, path, content, null, ct);

        if (response.IsSuccessStatusCode)
            return response;

        using (response)
            throw new ForgeException((int)response.StatusCode, await ErrorMessageAsync(response, ct));
    }

    public Task<HttpResponseMessage> ForwardAsync(HttpMethod method, string pathAndQuery, HttpContent? content, string? accept, CancellationToken ct) =>
        AuthorizedAsync(_http, method, pathAndQuery, content, accept, ct);

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

    public void Dispose()
    {
        _http.Dispose();

        if (!ReferenceEquals(_pinned, _http))
            _pinned.Dispose();

        _refreshGate.Dispose();
    }
}
