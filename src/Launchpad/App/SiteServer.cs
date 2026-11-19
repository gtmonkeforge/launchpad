using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Launchpad.Forge;
using Launchpad.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Launchpad.App;

public sealed class SiteServer : IAsyncDisposable
{
    public const int PreferredPort = 47615;
    private const string CookieName = "lp_session";
    private const long MaxUploadBytes = 512L * 1024 * 1024;

    private const string Policy = "default-src 'self'; img-src 'self' blob: data:; style-src 'self'; script-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";

    private readonly ForgeClient _forge;
    private readonly Func<object> _launchpadBoot;
    private readonly Func<Task<IReadOnlyCollection<string>>> _installed;
    private readonly string _launchToken = NewToken();
    private readonly string _cookie = NewToken();
    private WebApplication? _app;

    public SiteServer(ForgeClient forge, Func<object> launchpadBoot, Func<Task<IReadOnlyCollection<string>>>? installed = null)
    {
        _forge = forge;
        _launchpadBoot = launchpadBoot;
        _installed = installed ?? (() => Task.FromResult<IReadOnlyCollection<string>>([]));
    }

    public Action<string>? Log { get; init; }

    public Uri Origin { get; private set; } = new("http://127.0.0.1/");

    public string StartUrl(string path) => $"{Origin.GetLeftPart(UriPartial.Authority)}/?t={_launchToken}&next={Uri.EscapeDataString(path)}";

    private static string NewToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    public async Task StartAsync()
    {
        foreach (var port in new[] { PreferredPort, 0 })
        {
            WebApplication app = Build(port);

            try
            {
                await app.StartAsync();
            }
            catch (Exception) when (port != 0)
            {
                await app.DisposeAsync();
                continue;
            }

            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            Origin = new Uri(new Uri(address).GetLeftPart(UriPartial.Authority).Replace("localhost", "127.0.0.1") + "/");
            _app = app;
            return;
        }
    }

    private WebApplication Build(int port)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = MaxUploadBytes;
            options.Listen(IPAddress.Loopback, port);
        });

        WebApplication app = builder.Build();
        app.Run(async http =>
        {
            await HandleAsync(http);

            if (http.Response.StatusCode >= 400)
                Log?.Invoke($"{http.Response.StatusCode} {http.Request.Method} {http.Request.Path}{http.Request.QueryString}");
        });
        return app;
    }

    public static bool IsPage(string path) =>
        !path.StartsWith("/api/", StringComparison.Ordinal) && !path.StartsWith("/static/", StringComparison.Ordinal)
        && !path.StartsWith("/_launchpad/", StringComparison.Ordinal) && !path.StartsWith("/media/", StringComparison.Ordinal)
        && !path.StartsWith("/brand/", StringComparison.Ordinal) && path != "/favicon.ico";

    public enum ProxyRule
    {
        Allow,
        Deny,
        Logout,
    }

    public static ProxyRule Rule(string method, string path)
    {
        if (!path.StartsWith("/api/", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal))
            return ProxyRule.Deny;

        if (method == "POST" && path == "/api/v1/logout")
            return ProxyRule.Logout;

        if (path.StartsWith("/api/v1/auth/", StringComparison.Ordinal)
            || path.StartsWith("/api/v1/injector/challenge", StringComparison.Ordinal)
            || path.StartsWith("/api/v1/injector/payload", StringComparison.Ordinal)
            || (method != "GET" && method != "DELETE" && path.StartsWith("/api/v1/injector/", StringComparison.Ordinal))
            || path.StartsWith("/api/v1/stripe/", StringComparison.Ordinal)
            || path.StartsWith("/api/v1/setup", StringComparison.Ordinal))
            return ProxyRule.Deny;

        return ProxyRule.Allow;
    }

    private async Task HandleAsync(HttpContext http)
    {
        HttpRequest request = http.Request;
        HttpResponse response = http.Response;
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.XFrameOptions = "DENY";
        response.Headers["Referrer-Policy"] = "no-referrer";

        if (!string.Equals(request.Host.Value, Origin.Authority, StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = 421;
            return;
        }

        var path = request.Path.Value ?? "/";

        if (path == "/" && request.Query["t"] == _launchToken)
        {
            response.Cookies.Append(CookieName, _cookie, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/" });
            var next = request.Query["next"].FirstOrDefault() ?? "/browse";
            response.Redirect(next.StartsWith('/') && !next.StartsWith("//", StringComparison.Ordinal) ? next : "/browse");
            return;
        }

        if (request.Cookies[CookieName] != _cookie)
        {
            response.StatusCode = 403;

            if (HttpMethods.IsGet(request.Method) && IsPage(path))
            {
                var nonce = NewToken();
                response.ContentType = "text/html; charset=utf-8";
                response.Headers.CacheControl = "no-store";
                response.Headers.ContentSecurityPolicy = $"default-src 'none'; script-src 'nonce-{nonce}'; style-src 'nonce-{nonce}'";
                await response.WriteAsync($$"""
                    <!doctype html><html lang="en"><head><meta charset="utf-8"><title>Launchpad</title>
                    <style nonce="{{nonce}}">body{margin:0;min-height:100vh;display:grid;place-items:center;background:#111214;color:#9b9ea6;font:14px system-ui,sans-serif}</style></head>
                    <body><p>Reopening Launchpad…</p><script nonce="{{nonce}}">window.external?.sendMessage?.(JSON.stringify({ method: 'window.restart', args: { path: location.pathname + location.search } }));</script></body></html>
                    """, http.RequestAborted);
            }

            return;
        }

        var reading = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);

        if (!reading && request.Headers.Origin != Origin.GetLeftPart(UriPartial.Authority))
        {
            response.StatusCode = 403;
            return;
        }

        if (path.StartsWith("/api/", StringComparison.Ordinal))
        {
            await ProxyAsync(http, path);
            return;
        }

        if (path.StartsWith("/media/", StringComparison.Ordinal) || path.StartsWith("/brand/", StringComparison.Ordinal))
        {
            if (reading)
                await RelayAsync(http, HttpMethod.Get, path + request.QueryString, null);
            else
                response.StatusCode = 405;

            return;
        }

        if (!reading)
        {
            response.StatusCode = 405;
            return;
        }

        if (path.StartsWith("/static/", StringComparison.Ordinal) || path.StartsWith("/_launchpad/", StringComparison.Ordinal))
        {
            if (SiteAssets.Find(path) is not { } asset)
            {
                response.StatusCode = 404;
                return;
            }

            response.ContentType = asset.ContentType;
            response.Headers.CacheControl = "no-cache";
            await response.Body.WriteAsync(asset.Bytes, http.RequestAborted);
            return;
        }

        if (path == "/favicon.ico")
        {
            response.Redirect("/static/favicon.svg");
            return;
        }

        if (path == "/")
        {
            response.Redirect("/browse");
            return;
        }

        response.ContentType = "text/html; charset=utf-8";
        response.Headers.CacheControl = "no-store";
        response.Headers.ContentSecurityPolicy = Policy;
        object boot = new { brand = new { name = "MonkeForge", game = "Gorilla Tag", tint_favicon = true }, launchpad = _launchpadBoot() };
        await response.WriteAsync(SiteAssets.Shell(boot), http.RequestAborted);
    }

    private async Task ProxyAsync(HttpContext http, string path)
    {
        HttpRequest request = http.Request;
        HttpResponse response = http.Response;
        response.Headers.CacheControl = "no-store";

        switch (Rule(request.Method, path))
        {
            case ProxyRule.Deny:
                response.StatusCode = 403;
                await response.WriteAsJsonAsync(new { error = "Launchpad handles this itself." }, http.RequestAborted);
                return;

            case ProxyRule.Logout:
                await _forge.LogoutAsync(http.RequestAborted);
                await response.WriteAsJsonAsync(new { ok = true }, http.RequestAborted);
                return;
        }

        if (HttpMethods.IsGet(request.Method) && path == "/api/v1/mods" && request.Query["installed"] == "1")
        {
            await InstalledModsAsync(http);
            return;
        }

        HttpContent? content = null;

        if (request.ContentLength > 0 || request.Headers.TransferEncoding.Count > 0)
        {
            content = new StreamContent(request.Body);

            if (request.ContentType != null)
                content.Headers.TryAddWithoutValidation("Content-Type", request.ContentType);

            if (request.ContentLength is { } length)
                content.Headers.ContentLength = length;
        }

        await RelayAsync(http, new HttpMethod(request.Method), path + request.QueryString, content);
    }

    private async Task InstalledModsAsync(HttpContext http)
    {
        const int Batch = 100, MaxBatches = 20;
        IQueryCollection query = http.Request.Query;
        var page = Math.Max(1, int.TryParse(query["page"], out var p) ? p : 1);
        var limit = Math.Clamp(int.TryParse(query["limit"], out var l) ? l : 12, 1, 100);
        var installed = new HashSet<string>(await _installed(), StringComparer.Ordinal);
        var matches = new List<JsonNode>();

        var filters = string.Join('&', query.Where(pair => pair.Key is not ("page" or "limit" or "installed"))
            .SelectMany(pair => pair.Value.Select(value => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(value ?? ""))));

        try
        {
            for (var batch = 1; installed.Count > 0 && batch <= MaxBatches; batch++)
            {
                JsonNode result = await _forge.GetJsonAsync($"/api/v1/mods?{filters}{(filters.Length > 0 ? "&" : "")}page={batch}&limit={Batch}", http.RequestAborted);
                JsonArray items = result["items"] as JsonArray ?? [];
                matches.AddRange(items.Where(item => installed.Contains(item.Str("guid") ?? "")).Select(item => item!.DeepClone()));

                if (items.Count < Batch || batch * Batch >= result.Long("total"))
                    break;
            }
        }
        catch (ForgeException ex)
        {
            http.Response.StatusCode = ex.Status is >= 400 and < 600 ? ex.Status : 502;
            await http.Response.WriteAsJsonAsync(new { ex = ex.Message }, http.RequestAborted);
            return;
        }

        var body = new JsonObject
        {
            ["items"] = new JsonArray(matches.Skip((page - 1) * limit).Take(limit).ToArray()),
            ["total"] = matches.Count,
            ["page"] = page,
            ["limit"] = limit,
        };

        http.Response.ContentType = "application/json; charset=utf-8";
        await http.Response.WriteAsync(body.ToJsonString(), http.RequestAborted);
    }

    private async Task RelayAsync(HttpContext http, HttpMethod method, string pathAndQuery, HttpContent? content)
    {
        HttpResponse response = http.Response;
        HttpResponseMessage upstream;

        try
        {
            upstream = await _forge.ForwardAsync(method, pathAndQuery, content, http.Request.Headers.Accept.ToString(), http.RequestAborted);
        }
        catch (ForgeException ex)
        {
            response.StatusCode = 502;
            await response.WriteAsJsonAsync(new { ex = ex.Message }, http.RequestAborted);
            return;
        }

        using (upstream)
        {
            response.StatusCode = (int)upstream.StatusCode;

            foreach (var name in new[] { "Cache-Control", "ETag", "Last-Modified", "Retry-After" })
            {
                if (upstream.Headers.TryGetValues(name, out IEnumerable<string>? values))
                    response.Headers[name] = values.ToArray();
            }

            if (upstream.Headers.Location is { } location)
                response.Headers.Location = location.IsAbsoluteUri && location.GetLeftPart(UriPartial.Authority) == _forge.BaseUri.GetLeftPart(UriPartial.Authority) ? location.PathAndQuery : location.ToString();

            foreach (var name in new[] { "Content-Type", "Content-Disposition", "Content-Length" })
            {
                if (upstream.Content.Headers.TryGetValues(name, out IEnumerable<string>? values))
                    response.Headers[name] = values.ToArray();
            }

            if (!HttpMethods.IsHead(http.Request.Method))
                await upstream.Content.CopyToAsync(response.Body, http.RequestAborted);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null)
            await _app.DisposeAsync();
    }
}
