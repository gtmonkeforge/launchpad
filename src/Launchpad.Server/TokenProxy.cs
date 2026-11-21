using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace Launchpad.Server;

// Launchpad is registered on MonkeForge as a confidential OAuth client, and its secret has to stay off people's computers.
// The app does the browser half of the flow itself (loopback redirect + PKCE), then sends the code here; this adds the
// client secret and passes the request to MonkeForge's token endpoint unchanged otherwise.
//
// That makes this endpoint usable by anyone who has a code and its PKCE verifier, which only the Launchpad instance that
// started the sign-in has. The checks below keep it to exactly Launchpad's flow: loopback redirect, PKCE always, and
// only the grant types and parameters Launchpad sends.
public static partial class TokenProxy
{
    public const string HttpClientName = "forge";

    [GeneratedRegex(@"\Ahttp://127\.0\.0\.1:[0-9]{1,5}/callback\z")]
    private static partial Regex LoopbackRedirect();

    [GeneratedRegex(@"\A[A-Za-z0-9._~-]{43,128}\z")]
    private static partial Regex Verifier();

    [GeneratedRegex(@"\A[A-Za-z0-9_-]{20,100}\z")]
    private static partial Regex Code();

    [GeneratedRegex(@"\A(mfrt|mfat)_[A-Za-z0-9_-]{20,190}\z")]
    private static partial Regex Token();

    private static readonly string[] TokenFields = ["grant_type", "code", "redirect_uri", "code_verifier", "refresh_token", "scope", "client_id"];
    private static readonly string[] RevokeFields = ["token", "token_type_hint", "client_id"];

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/oauth/token", TokenAsync).RequireRateLimiting("oauth");
        app.MapPost("/api/v1/oauth/revoke", RevokeAsync).RequireRateLimiting("oauth");
    }

    private static IResult Error(string error, string description, int status = 400) =>
        Results.Json(new { error, error_description = description }, statusCode: status);

    private static async Task<Dictionary<string, string>?> ReadFormAsync(HttpRequest request, string[] allowed)
    {
        if (!request.HasFormContentType || request.ContentLength is > 16 * 1024)
            return null;

        IFormCollection form = await request.ReadFormAsync(request.HttpContext.RequestAborted);

        if (form.Files.Count > 0 || form.Any(pair => pair.Value.Count != 1 || !allowed.Contains(pair.Key)))
            return null;

        return form.ToDictionary(pair => pair.Key, pair => pair.Value[0] ?? "");
    }

    private static async Task<IResult> TokenAsync(HttpContext http, ServerOptions options, IHttpClientFactory clients)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";

        if (!options.TokensConfigured)
            return Error("temporarily_unavailable", "Sign-in isn't set up on this server yet.", 503);

        Dictionary<string, string>? values = await ReadFormAsync(http.Request, TokenFields);

        if (values == null)
            return Error("invalid_request", "Send the token request as application/x-www-form-urlencoded with Launchpad's parameters only.");

        if (values.TryGetValue("client_id", out var clientId) && clientId != options.ClientId)
            return Error("invalid_client", "This server only signs in Launchpad.", 401);

        var forward = new Dictionary<string, string>();

        switch (values.GetValueOrDefault("grant_type"))
        {
            case "authorization_code":
            {
                var code = values.GetValueOrDefault("code") ?? "";
                var redirect = values.GetValueOrDefault("redirect_uri") ?? "";
                var verifier = values.GetValueOrDefault("code_verifier") ?? "";

                if (!Code().IsMatch(code) || !LoopbackRedirect().IsMatch(redirect) || !Verifier().IsMatch(verifier) || values.ContainsKey("refresh_token") || values.ContainsKey("scope"))
                    return Error("invalid_request", "Send code, a loopback redirect_uri and a PKCE code_verifier.");

                forward["grant_type"] = "authorization_code";
                forward["code"] = code;
                forward["redirect_uri"] = redirect;
                forward["code_verifier"] = verifier;
                break;
            }

            case "refresh_token":
            {
                var refresh = values.GetValueOrDefault("refresh_token") ?? "";

                if (!refresh.StartsWith("mfrt_", StringComparison.Ordinal) || !Token().IsMatch(refresh) || values.Keys.Any(k => k is "code" or "redirect_uri" or "code_verifier"))
                    return Error("invalid_request", "Send a refresh_token.");

                forward["grant_type"] = "refresh_token";
                forward["refresh_token"] = refresh;

                if (values.TryGetValue("scope", out var scope) && scope.Length is > 0 and <= 200)
                    forward["scope"] = scope;

                break;
            }

            default:
                return Error("unsupported_grant_type", "Use grant_type=authorization_code or refresh_token.");
        }

        return await ForwardAsync(http, options, clients, "/api/v1/oauth/token", forward);
    }

    private static async Task<IResult> RevokeAsync(HttpContext http, ServerOptions options, IHttpClientFactory clients)
    {
        http.Response.Headers.CacheControl = "no-store";

        if (!options.TokensConfigured)
            return Error("temporarily_unavailable", "Sign-in isn't set up on this server yet.", 503);

        Dictionary<string, string>? values = await ReadFormAsync(http.Request, RevokeFields);
        var token = values?.GetValueOrDefault("token") ?? "";

        if (values == null || !Token().IsMatch(token))
            return Error("invalid_request", "Send the token to revoke.");

        if (values.TryGetValue("client_id", out var clientId) && clientId != options.ClientId)
            return Error("invalid_client", "This server only signs in Launchpad.", 401);

        return await ForwardAsync(http, options, clients, "/api/v1/oauth/revoke", new Dictionary<string, string> { ["token"] = token });
    }

    private static async Task<IResult> ForwardAsync(HttpContext http, ServerOptions options, IHttpClientFactory clients, string path, Dictionary<string, string> form)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.ForgeUrl, path)) { Content = new FormUrlEncodedContent(form) };

        // RFC 6749 2.3.1: both halves are form-encoded before they're joined for HTTP Basic.
        var credentials = Uri.EscapeDataString(options.ClientId) + ":" + Uri.EscapeDataString(options.ClientSecret);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;

        try
        {
            response = await clients.CreateClient(HttpClientName).SendAsync(request, http.RequestAborted);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !http.RequestAborted.IsCancellationRequested)
        {
            return Error("temporarily_unavailable", "MonkeForge couldn't be reached. Try again in a moment.", 502);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(http.RequestAborted);

            // A rejected client secret is this server's problem, not the user's: say so instead of passing invalid_client on.
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("TokenProxy").LogError("MonkeForge rejected the client credentials. Check LAUNCHPAD_CLIENT_ID and LAUNCHPAD_CLIENT_SECRET.");
                return Error("temporarily_unavailable", "Launchpad's sign-in server is misconfigured. Try again later.", 503);
            }

            if (response.Content.Headers.ContentType?.MediaType != "application/json")
                return Error("temporarily_unavailable", "MonkeForge had a problem. Try again in a moment.", 502);

            return Results.Content(body, "application/json; charset=utf-8", Encoding.UTF8, (int)response.StatusCode);
        }
    }
}
