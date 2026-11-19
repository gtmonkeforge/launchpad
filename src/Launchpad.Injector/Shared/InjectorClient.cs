#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

namespace MonkeForge.Injector;

public sealed class ReceivedMod
{
    public string Guid;
    public string Name;
    public string Version;
    public byte[] Dll;
}

public sealed class InjectorClient
{
    private static readonly TimeSpan WaitForLaunchpad = TimeSpan.FromMinutes(3);
    private readonly string _gameRoot;
    private readonly string _loader;
    private readonly Action<string> _info;
    private readonly Action<string> _warn;
    private readonly Queue<ReceivedMod> _ready = new Queue<ReceivedMod>();
    private readonly object _gate = new object();
    private static readonly Dictionary<string, Assembly> Loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
    private static bool _resolverInstalled;

    public InjectorClient(string gameRoot, string loader, Action<string> info, Action<string> warn)
    {
        _gameRoot = gameRoot;
        _loader = loader;
        _info = info;
        _warn = warn;
    }

    public void Start()
    {
        InstallResolver();
        var thread = new Thread(Run) { IsBackground = true, Name = "MonkeForge injector" };
        thread.Start();
    }

    public List<ReceivedMod> Drain()
    {
        lock (_gate)
        {
            var items = new List<ReceivedMod>(_ready);
            _ready.Clear();
            return items;
        }
    }

    public static Assembly Load(ReceivedMod mod)
    {
        try
        {
            Assembly assembly = Assembly.Load(mod.Dll);

            lock (Loaded)
                Loaded[assembly.GetName().Name] = assembly;

            return assembly;
        }
        finally
        {
            Array.Clear(mod.Dll, 0, mod.Dll.Length);
        }
    }

    public static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var types = new List<Type>();

            foreach (Type type in ex.Types)
            {
                if (type != null)
                    types.Add(type);
            }

            return types;
        }
    }

    private static void InstallResolver()
    {
        if (_resolverInstalled)
            return;

        _resolverInstalled = true;
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var name = new AssemblyName(args.Name).Name;

            lock (Loaded)
                return Loaded.TryGetValue(name, out Assembly found) ? found : null;
        };
    }

    private void Run()
    {
        DateTime deadline = DateTime.UtcNow + WaitForLaunchpad;
        var told = false;

        while (true)
        {
            try
            {
                if (TryFetch())
                    return;
            }
            catch (Exception ex)
            {
                _warn("Couldn't get injector mods from Launchpad: " + ex.Message);
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                _warn("Launchpad isn't running, so injector mods weren't loaded. Open Launchpad and restart the game.");
                return;
            }

            if (!told)
            {
                _info("Waiting for Launchpad to load injector mods…");
                told = true;
            }

            Thread.Sleep(2000);
        }
    }

    private bool TryFetch()
    {
        var path = Wire.ConfigPath(_gameRoot);

        if (!File.Exists(path))
            return false;

        Dictionary<string, string> config = Wire.DecodeFields(File.ReadAllText(path));

        if (!config.TryGetValue("port", out var portText) || !int.TryParse(portText, out var port) || port <= 0 || port > 65535 || !config.TryGetValue("token", out var token))
            return false;

        using (var client = new TcpClient())
        {
            try
            {
                client.Connect(IPAddress.Loopback, port);
            }
            catch (SocketException)
            {
                return false;
            }

            client.ReceiveTimeout = 120000;
            NetworkStream stream = client.GetStream();
            Wire.WriteFrame(stream, Wire.Hello, new Dictionary<string, string>
            {
                ["v"] = Wire.Version.ToString(),
                ["token"] = token,
                ["loader"] = _loader,
            });

            var count = 0;

            while (true)
            {
                Wire.Frame frame = Wire.ReadFrame(stream);

                if (frame.Type == Wire.End)
                {
                    var error = frame.Field("error");

                    if (error.Length > 0)
                        _warn(error);
                    else
                        _info(count == 1 ? "Received 1 injector mod from Launchpad." : "Received " + count + " injector mods from Launchpad.");

                    return true;
                }

                if (frame.Type == Wire.Error)
                {
                    _warn(frame.Field("name") + ": " + frame.Field("error"));
                    continue;
                }

                if (frame.Type != Wire.Mod)
                    throw new InvalidDataException("Launchpad sent an unexpected frame.");

                count++;

                lock (_gate)
                {
                    _ready.Enqueue(new ReceivedMod
                    {
                        Guid = frame.Field("guid"),
                        Name = frame.Field("name"),
                        Version = frame.Field("version"),
                        Dll = frame.Body,
                    });
                }
            }
        }
    }
}
