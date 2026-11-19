using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Launchpad.Forge;
using Launchpad.Infrastructure;
using Launchpad.Security;

namespace Launchpad.Injector;

public sealed class DeviceRegistry(ForgeClient forge, DeviceKeyStore keys, string file)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public sealed class Record
    {
        public string DeviceId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Storage { get; set; } = "";
        public string CreatedAt { get; set; } = "";
    }

    public string Slot()
    {
        var user = forge.UserId ?? throw new ForgeException(401, "Sign in to install injector mods.");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(forge.BaseUri.Host.ToLowerInvariant() + "|" + user));
        return Convert.ToHexStringLower(hash)[..24];
    }

    public static string DeviceName()
    {
        var name = "Launchpad on " + Environment.MachineName;
        return name.Length <= 60 ? name : name[..60];
    }

    public async Task<Record?> CurrentAsync()
    {
        if (forge.UserId == null)
            return null;

        Dictionary<string, Record> all = await Json.ReadFileAsync(file, () => new Dictionary<string, Record>());
        return all.GetValueOrDefault(Slot());
    }

    public async Task<(IDeviceKey Key, string DeviceId)> EnsureAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);

        try
        {
            var slot = Slot();
            Dictionary<string, Record> all = await Json.ReadFileAsync(file, () => new Dictionary<string, Record>());
            IDeviceKey? key = keys.Open(slot);

            if (key != null && all.TryGetValue(slot, out Record? record) && record.DeviceId.Length > 0)
                return (key, record.DeviceId);

            key ??= keys.Create(slot);
            string id;

            try
            {
                id = await RegisterAsync(key, ct);
            }
            catch (ForgeException ex) when (ex.Status == 409 && ex.Message.Contains("already registered", StringComparison.OrdinalIgnoreCase))
            {
                key.Dispose();
                key = keys.Create(slot);
                id = await RegisterAsync(key, ct);
            }
            catch
            {
                key.Dispose();
                throw;
            }

            all[slot] = new Record { DeviceId = id, Name = DeviceName(), Storage = key.Storage, CreatedAt = DateTimeOffset.UtcNow.ToString("O") };
            await Json.WriteFileAsync(file, all);
            return (key, id);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> RegisterAsync(IDeviceKey key, CancellationToken ct)
    {
        JsonNode created = await forge.SendJsonAsync(HttpMethod.Post, "/api/v1/injector/devices", new { name = DeviceName(), public_key = key.PublicKey }, true, ct);
        return created.Str("id") ?? throw new ForgeException(500, "MonkeForge didn't return a device ID.");
    }

    public async Task ResetAsync()
    {
        await _lock.WaitAsync();

        try
        {
            var slot = Slot();
            keys.Delete(slot);
            Dictionary<string, Record> all = await Json.ReadFileAsync(file, () => new Dictionary<string, Record>());

            if (all.Remove(slot))
                await Json.WriteFileAsync(file, all);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<JsonNode> ListAsync(CancellationToken ct) => forge.GetJsonAsync("/api/v1/injector/devices", ct);

    public async Task RevokeAsync(string deviceId, CancellationToken ct)
    {
        await forge.SendJsonAsync(HttpMethod.Delete, "/api/v1/injector/devices/" + Uri.EscapeDataString(deviceId), null, true, ct);

        if ((await CurrentAsync())?.DeviceId == deviceId)
            await ResetAsync();
    }
}
