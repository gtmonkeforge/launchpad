using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Launchpad.Server;
using Launchpad.Updates;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.StaticFiles;

// launchpad.monkeforge.org: signs Launchpad in to MonkeForge (TokenProxy) and hands out Launchpad updates.

ServerOptions options = ServerOptions.Load();
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("URLS")))
    builder.WebHost.UseUrls("http://127.0.0.1:5080");

builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = options.MaxUploadBytes);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<ReleaseStore>();
builder.Services.AddHttpClient(TokenProxy.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MonkeForge-Launchpad-Server", typeof(ServerOptions).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"));
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });

string ClientAddress(HttpContext http)
{
    if (options.TrustCloudflare && http.Request.Headers.TryGetValue("CF-Connecting-IP", out var forwarded) && forwarded.ToString() is { Length: > 0 } ip)
        return ip;

    return http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = 429;
    limiter.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = "slow_down", error_description = "Too many requests. Wait a minute and try again." }, ct);
    };
    limiter.AddPolicy("oauth", http => RateLimitPartition.GetFixedWindowLimiter(ClientAddress(http), _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
    limiter.AddPolicy("updates", http => RateLimitPartition.GetFixedWindowLimiter(ClientAddress(http), _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
    limiter.AddPolicy("publish", http => RateLimitPartition.GetFixedWindowLimiter(ClientAddress(http), _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(10) }));
});

builder.Services.AddRequestTimeouts();

WebApplication app = builder.Build();

if (!options.TokensConfigured)
    app.Logger.LogWarning("LAUNCHPAD_CLIENT_SECRET isn't set, so sign-in won't work until it is.");

if (options.ReleasePublicKey.Length == 0)
    app.Logger.LogWarning("RELEASE_PUBLIC_KEY isn't set, so uploads aren't checked against the release key. Launchpad still checks the signature before installing.");

app.Use(async (http, next) =>
{
    http.Response.Headers.XContentTypeOptions = "nosniff";
    http.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next(http);
});

app.UseRateLimiter();
app.UseRequestTimeouts();

app.MapGet("/", () => Results.Redirect(options.HomepageUrl));
app.MapGet("/api/v1/health", () => Results.Json(new { status = "ok", sign_in = options.TokensConfigured }));

TokenProxy.Map(app);

// What Launchpad asks on start and every few hours. The manifest goes back as base64 so the app verifies the exact
// bytes that were signed. 204 means there's nothing for this platform yet.
app.MapGet("/api/v1/updates/latest", (string? os, string? arch, ReleaseStore releases, HttpResponse response) =>
{
    os ??= "windows";
    arch ??= "x64";

    if (!ReleaseManifest.Platforms.Contains(os) || !ReleaseManifest.Architectures.Contains(arch))
        return Results.Json(new { error = "Unknown os or arch." }, statusCode: 400);

    response.Headers.CacheControl = "public, max-age=300";
    ReleaseStore.Release? latest = releases.Latest(os, arch);

    if (latest == null)
        return Results.NoContent();

    return Results.Json(new
    {
        version = latest.Manifest.Version,
        manifest = Convert.ToBase64String(latest.ManifestBytes),
        signature = latest.Signature,
    });
}).RequireRateLimiting("updates");

app.MapGet("/api/v1/releases", (ReleaseStore releases) => Results.Json(new
{
    items = releases.All().Select(r => new
    {
        version = r.Manifest.Version,
        published_at = r.Manifest.PublishedAt,
        notes = r.Manifest.Notes,
        files = r.Manifest.Files.Select(f => new { f.Os, f.Arch, f.Name, f.Size, f.Sha256, url = $"/download/{r.Manifest.Version}/{f.Name}" }),
    }),
})).RequireRateLimiting("updates");

// Stable links for the website's download buttons, e.g. /download/latest/windows.
app.MapGet("/download/latest/{os}", (string os, string? arch, ReleaseStore releases) =>
{
    arch ??= "x64";
    ReleaseStore.Release? latest = ReleaseManifest.Platforms.Contains(os) ? releases.Latest(os, arch) : null;
    ReleaseFile? file = latest?.Manifest.FileFor(os, arch);

    return latest == null || file == null
        ? Results.NotFound(new { error = "There's no release for that platform yet." })
        : Results.Redirect($"/download/{latest.Manifest.Version}/{file.Name}");
});

var contentTypes = new FileExtensionContentTypeProvider();

app.MapGet("/download/{version}/{name}", (string version, string name, ReleaseStore releases, HttpResponse response) =>
{
    ReleaseStore.Release? release = releases.Find(version);
    ReleaseFile? file = release?.Manifest.Files.FirstOrDefault(f => f.Name == name);

    if (release == null || file == null)
        return Results.NotFound(new { error = "That release file doesn't exist." });

    response.Headers.CacheControl = "public, max-age=31536000, immutable";
    response.Headers["X-Content-SHA256"] = file.Sha256;
    var type = contentTypes.TryGetContentType(file.Name, out var known) ? known : "application/octet-stream";
    return Results.File(Path.Combine(release.Folder, file.Name), type, file.Name, enableRangeProcessing: true);
});

app.MapPut("/api/v1/releases/{version}", async (string version, HttpContext http, ReleaseStore releases) =>
{
    if (!options.PublishingEnabled)
        return Results.Json(new { error = "Publishing is turned off. Set PUBLISH_TOKEN (at least 32 characters) to turn it on." }, statusCode: 403);

    var header = http.Request.Headers.Authorization.ToString();
    var supplied = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..].Trim() : "";

    if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes(options.PublishToken))))
        return Results.Json(new { error = "Send the publish token as Authorization: Bearer <token>." }, statusCode: 401);

    if (!http.Request.HasFormContentType)
        return Results.Json(new { error = "Upload the release as multipart/form-data." }, statusCode: 400);

    if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        limit.MaxRequestBodySize = options.MaxUploadBytes;

    try
    {
        IFormCollection form = await http.Request.ReadFormAsync(http.RequestAborted);
        await releases.PublishAsync(version, form, http.RequestAborted);
        return Results.Json(new { ok = true, version }, statusCode: 201);
    }
    catch (ReleaseStore.PublishException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: ex.Status);
    }
    catch (InvalidDataException ex)
    {
        return Results.Json(new { error = "The upload couldn't be read: " + ex.Message }, statusCode: 400);
    }
}).RequireRateLimiting("publish").WithRequestTimeout(TimeSpan.FromMinutes(30));

app.MapFallback(() => Results.NotFound(new { error = "Not found." }));

app.Run();
