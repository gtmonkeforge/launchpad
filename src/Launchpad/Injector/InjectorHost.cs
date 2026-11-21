using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Launchpad.Forge;
using Launchpad.Game;
using Launchpad.Infrastructure;
using Launchpad.Mods;
using MonkeForge.Injector;

namespace Launchpad.Injector;

public sealed class InjectorHost : IDisposable
{
    private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(10);

    private readonly PayloadClient _payloads;
    private readonly Func<string?> _game;
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly string _token = OAuthSignIn.Base64Url(RandomNumberGenerator.GetBytes(32));
    private readonly object _gate = new();
    private string? _published;

    public InjectorHost(PayloadClient payloads, Func<string?> game)
    {
        _payloads = payloads;
        _game = game;
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public event Action<string, bool>? Status;

    internal Func<IPEndPoint, IPEndPoint, bool?> OwnsConnection { get; init; } = GameProcess.OwnsConnection;
    internal Func<bool> GameRunning { get; init; } = GameProcess.IsRunning;

    public void Start()
    {
        _listener.Start(8);
        _ = AcceptLoopAsync();
        Publish();
    }

    public void Publish()
    {
        lock (_gate)
        {
            var game = _game();
            var target = GameLocator.IsGameFolder(game) ? Wire.ConfigPath(game!) : null;

            if (_published != null && _published != target)
                Unpublish(_published);

            if (target == null)
            {
                _published = null;
                return;
            }

            try
            {
                Archive.EnsureNoLinks(game!, target);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var text = Wire.EncodeFields(new Dictionary<string, string> { ["port"] = Port.ToString(), ["token"] = _token, ["pid"] = Environment.ProcessId.ToString() });
                var temp = target + ".tmp";
                File.WriteAllText(temp, text);
                File.Move(temp, target, true);
                _published = target;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or UserFacingException)
            {
                _published = null;
                Status?.Invoke("Launchpad couldn't write to the game folder, so injector mods won't load: " + ex.Message, true);
            }
        }
    }

    private void Unpublish(string path)
    {
        try
        {
            if (File.Exists(path) && Wire.DecodeFields(File.ReadAllText(path)).GetValueOrDefault("token") == _token)
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException)
            {
                continue;
            }

            _ = Task.Run(async () =>
            {
                using (client)
                {
                    try
                    {
                        await HandleAsync(client, _stop.Token);
                    }
                    catch (Exception ex) when (ex is IOException or SocketException or InvalidDataException or OperationCanceledException)
                    {
                    }
                }
            });
        }
    }

    internal async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        NetworkStream stream = client.GetStream();
        var remote = (IPEndPoint)client.Client.RemoteEndPoint!;
        var local = (IPEndPoint)client.Client.LocalEndPoint!;

        using var helloTimer = new CancellationTokenSource(HelloTimeout);
        using CancellationTokenRegistration timeout = helloTimer.Token.Register(client.Close);
        Wire.Frame hello = await Task.Run(() => Wire.ReadFrame(stream), ct);
        timeout.Unregister();

        if (hello.Type != Wire.Hello || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hello.Field("token")), Encoding.UTF8.GetBytes(_token)))
            return;

        var owner = OwnsConnection(remote, local);

        if (owner == false || (owner == null && !GameRunning()))
            return;

        ModLoader? loader = Loaders.Parse(hello.Field("loader"));
        var game = _game();

        if (loader == null || !GameLocator.IsGameFolder(game))
        {
            Send(stream, Wire.End, new() { ["error"] = "Launchpad doesn't have this game folder selected." });
            return;
        }

        Manifest manifest = await Manifest.ReadAsync(game!);
        List<InstalledMod> mods = manifest.Installed.Where(mod => mod.Injector && mod.EffectiveLoader == loader).ToList();
        var loaded = 0;

        foreach (InstalledMod mod in mods)
        {
            try
            {
                using Payload payload = await _payloads.FetchAsync(mod.Guid, mod.Channel ?? "stable", loader.Value.Id(), ct);
                Wire.WriteFrame(stream, Wire.Mod, new Dictionary<string, string> { ["guid"] = payload.Guid, ["name"] = mod.Name, ["version"] = payload.Version }, payload.Dll);
                loaded++;
            }
            catch (UserFacingException ex)
            {
                Send(stream, Wire.Error, new() { ["guid"] = mod.Guid, ["name"] = mod.Name, ["error"] = ex.Message });
                Status?.Invoke($"{mod.Name} couldn't be sent to the game: {ex.Message}", true);
            }
        }

        Send(stream, Wire.End, []);

        if (mods.Count > 0)
            Status?.Invoke(loaded == 1 ? "Gorilla Tag loaded 1 injector mod." : $"Gorilla Tag loaded {loaded} injector mods.", loaded < mods.Count);
    }

    private static void Send(Stream stream, byte type, Dictionary<string, string> fields) => Wire.WriteFrame(stream, type, fields);

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();

        lock (_gate)
        {
            if (_published != null)
                Unpublish(_published);
        }

        _stop.Dispose();
    }
}
