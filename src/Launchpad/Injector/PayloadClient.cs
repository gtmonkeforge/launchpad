using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Launchpad.Forge;
using Launchpad.Infrastructure;
using Launchpad.Security;

namespace Launchpad.Injector;

public sealed class Payload(string guid, string version, string fileId, byte[] dll) : IDisposable
{
    public string Guid { get; } = guid;
    public string Version { get; } = version;
    public string FileId { get; } = fileId;
    public byte[] Dll { get; } = dll;
    public string Sha256 { get; } = Convert.ToHexStringLower(SHA256.HashData(dll));

    public void Dispose() => CryptographicOperations.ZeroMemory(Dll);
}

public sealed class PayloadClient(ForgeClient forge, DeviceRegistry devices)
{
    private const int MaxEnvelopeBytes = 64 * 1024 * 1024;

    public async Task<Payload> FetchAsync(string guid, string channel, string loader, CancellationToken ct)
    {
        if (Hardening.DebuggerAttached())
            throw new UserFacingException("Injector mods can't be installed while a debugger is attached to Launchpad.");

        for (var attempt = 0; ; attempt++)
        {
            (IDeviceKey key, var deviceId) = await devices.EnsureAsync(ct);

            try
            {
                return await AttemptAsync(key, deviceId, guid, channel, loader, ct);
            }
            catch (ForgeException ex) when (ex.Status == 401 && attempt < 2 && forge.User != null)
            {
                if (attempt == 1)
                    await devices.ResetAsync();
            }
            finally
            {
                key.Dispose();
            }
        }
    }

    private async Task<Payload> AttemptAsync(IDeviceKey key, string deviceId, string guid, string channel, string loader, CancellationToken ct)
    {
        JsonNode challenge = await forge.SendJsonAsync(HttpMethod.Post, "/api/v1/injector/challenge", new { }, true, ct);
        var nonce = challenge.Str("nonce") ?? throw new ForgeException(500, "MonkeForge didn't return a challenge.");

        using ECDiffieHellman ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var clientKey = Convert.ToBase64String(ephemeral.ExportSubjectPublicKeyInfo());
        var signature = Convert.ToBase64String(key.Sign(Encoding.UTF8.GetBytes(Envelope.SignedMessage(nonce, deviceId, guid, channel, loader, clientKey))));

        var body = new Dictionary<string, string>
        {
            ["device_id"] = deviceId,
            ["guid"] = guid,
            ["channel"] = channel,
            ["loader"] = loader,
            ["client_key"] = clientKey,
            ["nonce"] = nonce,
            ["signature"] = signature,
        };

        HttpResponseMessage response;

        try
        {
            response = await forge.SendAsync(HttpMethod.Post, "/api/v1/injector/payload", JsonContent.Create(body), true, ct);
        }
        catch (ForgeException ex) when (ex.Status == 402)
        {
            throw new ForgeException(402, "You don't own this mod yet. Buy it on its page, then try again.");
        }

        using (response)
        {
            string Header(string name) => response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.First() : throw new ForgeException(500, "MonkeForge's payload response was missing " + name + ".");

            var version = Header("X-Forge-Version");
            var fileId = Header("X-Forge-File");
            var sha256 = Header("X-Forge-Sha256").ToLowerInvariant();

            if (Header("X-Forge-Guid") != guid)
                throw new ForgeException(500, "MonkeForge sent a payload for a different mod.");

            if (response.Content.Headers.ContentLength > MaxEnvelopeBytes)
                throw new ForgeException(413, "The mod is too large to install.");

            var blob = await ReadLimitedAsync(response.Content, ct);
            byte[] dll;

            try
            {
                dll = Envelope.Open(blob, ephemeral, Envelope.Aad(guid, version, fileId, deviceId, nonce));
            }
            catch (CryptographicException)
            {
                throw new UserFacingException("The mod couldn't be decrypted. It may have been changed in transit. Try again.");
            }

            var payload = new Payload(guid, version, fileId, dll);

            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(payload.Sha256), Encoding.ASCII.GetBytes(sha256)))
            {
                payload.Dispose();
                throw new UserFacingException("The mod failed its integrity check, so it wasn't installed.");
            }

            return payload;
        }
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, CancellationToken ct)
    {
        await using Stream stream = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxEnvelopeBytes)
                throw new ForgeException(413, "The mod is too large to install.");

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
