using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Launchpad.Forge;
using Launchpad.Game;
using Launchpad.Infrastructure;
using Photino.NET;

namespace Launchpad.App;

public sealed partial class AppHost
{
    private readonly LaunchpadApp _app;
    private readonly SiteServer _site;
    private readonly Bridge _bridge;
    private readonly PhotinoWindow _window;
    private readonly CancellationTokenSource _closing = new();

    private readonly string _token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    private readonly object _postGate = new();
    private readonly List<string> _queued = [];
    private bool _uiReady;
    private bool _closed;

    private Rectangle? _restoreBounds;
    private DateTime _lastRestart = DateTime.MinValue;

    private static readonly bool Debug = System.Diagnostics.Debugger.IsAttached || Environment.GetEnvironmentVariable("LAUNCHPAD_DEBUG") == "1";

    private static void Log(string message)
    {
        if (Debug)
            File.AppendAllText(Path.Combine(AppPaths.Root, "debug.log"), DateTime.Now.ToString("HH:mm:ss.fff ") + message + Environment.NewLine);
    }

    public AppHost(LaunchpadApp app)
    {
        _app = app;
        _site = new SiteServer(app.Forge, () => new
        {
            token = _token,
            version = typeof(AppHost).Assembly.GetName().Version?.ToString(3) ?? "2.0.0",
            os = OperatingSystem.IsWindows() ? "windows" : "linux",
            forge = app.Forge.BaseUri.GetLeftPart(UriPartial.Authority),
            maximized = _restoreBounds != null,
        }, async () => (await app.Mods.ListAsync()).Select(mod => mod.Guid).ToList())
        {
            Log = message => Log("http " + message),
        };

        _window = new PhotinoWindow()
            .SetTitle("MonkeForge Launchpad")
            .SetChromeless(true)
            .SetResizable(true)
            .SetUseOsDefaultSize(false)
            .SetUseOsDefaultLocation(false)
            .SetSize(1280, 820)
            .SetMinSize(900, 600)
            .Center()
            .SetContextMenuEnabled(Debug)
            .SetDevToolsEnabled(Debug)
            .RegisterWebMessageReceivedHandler(OnMessage)
            .RegisterWindowCreatedHandler((_, _) => OnCreated());

        if (OperatingSystem.IsWindows())
        {
            _window.SetTemporaryFilesPath(Path.Combine(AppPaths.Root, "webview"));
        }

        if (WriteIcon() is { } icon)
            _window.SetIconFile(icon);

        _bridge = new Bridge(app, PickFolderAsync);
        app.Event += (name, data) => Post(new JsonObject { ["event"] = name, ["data"] = JsonSerializer.SerializeToNode(data, Json.Options) });
    }

    public void Run()
    {
        _site.StartAsync().GetAwaiter().GetResult();
        var start = GameLocator.IsGameFolder(_app.Settings.GamePath) ? "/browse" : "/launchpad/game";
        _window.Load(new Uri(_site.StartUrl(start)));

        _ = Task.Run(async () =>
        {
            try
            {
                await _app.Forge.RestoreAsync(_closing.Token);
            }
            catch (Exception)
            {
            }

            _app.Emit("session", new { user = _app.Forge.User, restore_error = _app.Forge.RestoreError });
        });

        _window.WaitForClose();

        lock (_postGate)
            _closed = true;

        _closing.Cancel();
        _site.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void OnCreated()
    {
        if (OperatingSystem.IsWindows())
            Win32.RoundCorners(_window.WindowHandle);
    }

    private static string? WriteIcon()
    {
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("launchpad.ico");

            if (stream == null)
                return null;

            Directory.CreateDirectory(AppPaths.Root);
            var path = Path.Combine(AppPaths.Root, "launchpad.ico");

            using (FileStream file = File.Create(path))
                stream.CopyTo(file);

            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void OnMessage(object? sender, string message)
    {
        JsonObject? request;

        try
        {
            request = JsonNode.Parse(message) as JsonObject;
        }
        catch (JsonException)
        {
            return;
        }

        if (request == null)
            return;

        if (request.Str("method") == "window.restart")
        {
            if (DateTime.UtcNow - _lastRestart > TimeSpan.FromSeconds(10))
            {
                _lastRestart = DateTime.UtcNow;
                var path = (request["args"] as JsonObject).Str("path") ?? "/browse";
                Log("restart " + path);
                _window.Load(new Uri(_site.StartUrl(path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal) ? path : "/browse")));
            }

            return;
        }

        if (request.Str("token") != _token)
            return;

        var method = request.Str("method") ?? "";
        JsonObject args = request["args"] as JsonObject ?? new JsonObject();
        Log("call " + method);

        if (method.StartsWith("window.", StringComparison.Ordinal))
        {
            WindowCommand(method, args);
            return;
        }

        if (request["id"] is not JsonValue idValue || !idValue.TryGetValue(out long id))
            return;

        List<string> flush;

        lock (_postGate)
        {
            _uiReady = true;
            flush = [.. _queued];
            _queued.Clear();
        }

        foreach (var queued in flush)
            Send(queued);

        _ = Task.Run(async () =>
        {
            var reply = new JsonObject { ["id"] = id };

            try
            {
                var result = await _bridge.DispatchAsync(method, (JsonObject)args.DeepClone(), _closing.Token);
                reply["ok"] = true;
                reply["result"] = result is JsonNode node ? node.DeepClone() : JsonSerializer.SerializeToNode(result, Json.Options);
            }
            catch (Exception ex)
            {
                reply["ok"] = false;
                reply["status"] = ex is ForgeException forge ? forge.Status : 0;
                reply["error"] = ex is UserFacingException ? ex.Message : "Something went wrong: " + ex.Message;
            }

            Post(reply);

            if (method is "mods.install" or "mods.uninstall" or "loader.install")
                _app.Emit("progress", new { message = "" });
        });
    }

    private void WindowCommand(string method, JsonObject args)
    {
        switch (method)
        {
            case "window.minimize":
                _window.SetMinimized(true);
                break;

            case "window.toggleMaximize":
                ToggleMaximize();
                break;

            case "window.close":
                _window.Close();
                break;

            case "window.drag" when _restoreBounds == null:
                if (OperatingSystem.IsWindows())
                    Win32.StartMove(_window.WindowHandle);

                break;

            case "window.resize" when _restoreBounds == null:
                if (OperatingSystem.IsWindows())
                    Win32.StartResize(_window.WindowHandle, args.Str("edge") ?? "");

                break;

            case "window.moveBy" when _restoreBounds == null:
            {
                Point at = _window.Location;
                _window.SetLocation(new Point(at.X + (int)args.Long("dx"), at.Y + (int)args.Long("dy")));
                break;
            }

            case "window.resizeBy" when _restoreBounds == null:
                ResizeBy(args.Str("edge") ?? "", (int)args.Long("dx"), (int)args.Long("dy"));
                break;
        }
    }

    private void ToggleMaximize()
    {
        if (_restoreBounds is { } restore)
        {
            _restoreBounds = null;
            _window.SetSize(restore.Size);
            _window.SetLocation(restore.Location);
        }
        else
        {
            _restoreBounds = new Rectangle(_window.Location, _window.Size);
            Rectangle area = _window.MainMonitor.WorkArea;
            _window.SetLocation(area.Location);
            _window.SetSize(area.Size);
        }

        _app.Emit("window", new { maximized = _restoreBounds != null });
    }

    private void ResizeBy(string edge, int dx, int dy)
    {
        Point at = _window.Location;
        Size size = _window.Size;
        int left = at.X, top = at.Y, width = size.Width, height = size.Height;

        if (edge.Contains('e'))
            width += dx;

        if (edge.Contains('s'))
            height += dy;

        if (edge.Contains('w'))
        {
            left += dx;
            width -= dx;
        }

        if (edge.Contains('n'))
        {
            top += dy;
            height -= dy;
        }

        if (width < 900 || height < 600)
            return;

        _window.SetLocation(new Point(left, top));
        _window.SetSize(new Size(width, height));
    }

    [SupportedOSPlatform("windows")]
    private static partial class Win32
    {
        private const uint WmSysCommand = 0x0112;
        private const int ScSize = 0xF000;
        private const int ScDragMove = 0xF012;

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ReleaseCapture();

        [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
        private static partial nint SendMessage(nint window, uint message, nint wParam, nint lParam);

        [LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

        public static void StartMove(nint window)
        {
            ReleaseCapture();
            SendMessage(window, WmSysCommand, ScDragMove, 0);
        }

        public static void StartResize(nint window, string edge)
        {
            var side = edge switch
            {
                "w" => 1,
                "e" => 2,
                "n" => 3,
                "nw" => 4,
                "ne" => 5,
                "s" => 6,
                "sw" => 7,
                "se" => 8,
                _ => 0,
            };

            if (side == 0)
                return;

            ReleaseCapture();
            SendMessage(window, WmSysCommand, ScSize + side, 0);
        }

        public static void RoundCorners(nint window)
        {
            var round = 2;
            DwmSetWindowAttribute(window, 33, ref round, sizeof(int));
        }
    }

    private void Post(JsonObject message)
    {
        var text = message.ToJsonString();

        lock (_postGate)
        {
            if (_closed)
                return;

            if (!_uiReady)
            {
                _queued.Add(text);
                return;
            }
        }

        Send(text);
    }

    private void Send(string text)
    {
        try
        {
            _window.Invoke(() => _window.SendWebMessage(text));
        }
        catch (Exception)
        {
        }
    }

    private Task<string?> PickFolderAsync(string title)
    {
        var done = new TaskCompletionSource<string?>();

        _window.Invoke(() =>
        {
            try
            {
                var chosen = _window.ShowOpenFolder(title, null, false);
                done.SetResult(chosen is { Length: > 0 } ? chosen[0] : null);
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        });

        return done.Task;
    }
}
