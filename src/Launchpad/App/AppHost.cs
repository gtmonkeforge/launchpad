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

    private volatile bool _updateLaunched;

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
            maximized = IsMaximized,
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

        _bridge = new Bridge(app, PickFolderAsync, () =>
        {
            _updateLaunched = true;
            _window.Close();
        });
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

        _app.Updates.Start(() => _app.Settings.AutoUpdate, _closing.Token);

        _window.WaitForClose();

        lock (_postGate)
            _closed = true;

        _closing.Cancel();
        _site.DisposeAsync().AsTask().GetAwaiter().GetResult();

        // A downloaded update the person didn't install yet goes in quietly once Launchpad has exited.
        if (!_updateLaunched && _app.Settings.AutoUpdate)
            _app.Updates.LaunchInstaller(relaunch: false);
    }

    private bool IsMaximized => OperatingSystem.IsWindows()
        ? _window.WindowHandle != 0 && Win32.IsMaximized(_window.WindowHandle)
        : _restoreBounds != null;

    private void OnCreated()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // A real caption and thick frame let Windows handle Snap, drag-to-restore and the minimize/maximize animations.
        Win32.UseNativeFrame(_window.WindowHandle, maximized => Task.Run(() => _app.Emit("window", new { maximized })));
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

            if (method is "mods.install" or "mods.installFile" or "mods.uninstall" or "loader.install")
                _app.Emit("progress", new { message = "" });
        });
    }

    private void WindowCommand(string method, JsonObject args)
    {
        var windows = OperatingSystem.IsWindows();

        switch (method)
        {
            case "window.minimize":
                if (windows)
                    Win32.Minimize(_window.WindowHandle);
                else
                    _window.SetMinimized(true);

                break;

            case "window.toggleMaximize":
                if (windows)
                    Win32.ToggleMaximize(_window.WindowHandle);
                else
                    ToggleMaximize();

                break;

            case "window.close":
                _window.Close();
                break;

            // On Windows, dragging a maximized window restores it and snaps like a native title bar.
            case "window.drag" when windows:
                Win32.StartMove(_window.WindowHandle);
                break;

            case "window.resize" when windows && !IsMaximized:
                Win32.StartResize(_window.WindowHandle, args.Str("edge") ?? "");
                break;

            case "window.moveBy" when !windows && _restoreBounds == null:
            {
                Point at = _window.Location;
                _window.SetLocation(new Point(at.X + (int)args.Long("dx"), at.Y + (int)args.Long("dy")));
                break;
            }

            case "window.resizeBy" when !windows && _restoreBounds == null:
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
    private static unsafe partial class Win32
    {
        private const uint WmSize = 0x0005;
        private const uint WmNcCalcSize = 0x0083;
        private const uint WmSysCommand = 0x0112;
        private const int ScSize = 0xF000;
        private const int ScDragMove = 0xF012;
        private const int GwlStyle = -16;
        private const int GwlpWndProc = -4;
        private const long WsMaximizeBox = 0x00010000, WsMinimizeBox = 0x00020000, WsThickFrame = 0x00040000, WsSysMenu = 0x00080000, WsCaption = 0x00C00000, WsPopup = 0x80000000;
        private const uint SwpNoSize = 0x1, SwpNoMove = 0x2, SwpNoZOrder = 0x4, SwpNoActivate = 0x10, SwpFrameChanged = 0x20;
        private const int SwMaximize = 3, SwMinimize = 6, SwRestore = 9;
        private const uint MonitorDefaultToNearest = 2;

        private static nint _previous;
        private static Action<bool>? _stateChanged;
        private static bool? _lastMaximized;

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect Work;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Margins
        {
            public int Left, Right, Top, Bottom;
        }

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ReleaseCapture();

        [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
        private static partial nint SendMessage(nint window, uint message, nint wParam, nint lParam);

        [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static partial nint CallWindowProc(nint previous, nint window, uint message, nint wParam, nint lParam);

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static partial nint GetWindowLongPtr64(nint window, int index);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static partial nint SetWindowLongPtr64(nint window, int index, nint value);

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static partial int GetWindowLong32(nint window, int index);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static partial int SetWindowLong32(nint window, int index, int value);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(nint window, int command);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsZoomed(nint window);

        [LibraryImport("user32.dll")]
        private static partial nint MonitorFromWindow(nint window, uint flags);

        [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

        [LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

        [LibraryImport("dwmapi.dll")]
        private static partial int DwmExtendFrameIntoClientArea(nint window, ref Margins margins);

        private static nint GetLong(nint window, int index) => nint.Size == 8 ? GetWindowLongPtr64(window, index) : GetWindowLong32(window, index);

        private static nint SetLong(nint window, int index, nint value) => nint.Size == 8 ? SetWindowLongPtr64(window, index, value) : SetWindowLong32(window, index, (int)value);

        public static bool IsMaximized(nint window) => IsZoomed(window);

        public static void Minimize(nint window) => ShowWindow(window, SwMinimize);

        public static void ToggleMaximize(nint window) => ShowWindow(window, IsZoomed(window) ? SwRestore : SwMaximize);

        // Gives the chromeless window a normal caption and thick frame, then hides that frame by treating the whole
        // window as client area. Windows then handles it like any other window for Snap, Win+arrow keys,
        // drag-to-restore and the minimize/maximize animations.
        public static void UseNativeFrame(nint window, Action<bool> stateChanged)
        {
            if (window == 0 || _previous != 0)
                return;

            _stateChanged = stateChanged;
            var style = (long)GetLong(window, GwlStyle);
            style = (style & ~WsPopup) | WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox;
            SetLong(window, GwlStyle, (nint)style);

            _previous = SetLong(window, GwlpWndProc, (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&WndProc);

            // A one-pixel frame keeps the DWM drop shadow.
            var margins = new Margins { Left = 1, Right = 1, Top = 1, Bottom = 1 };
            DwmExtendFrameIntoClientArea(window, ref margins);
            SetWindowPos(window, 0, 0, 0, 0, 0, SwpFrameChanged | SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate);
        }

        [UnmanagedCallersOnly]
        private static nint WndProc(nint window, uint message, nint wParam, nint lParam)
        {
            if (message == WmNcCalcSize && wParam != 0)
            {
                // A maximized window overhangs the monitor by its frame width, so pin the content to the work area.
                if (IsZoomed(window))
                {
                    var info = new MonitorInfo { Size = sizeof(MonitorInfo) };

                    if (GetMonitorInfo(MonitorFromWindow(window, MonitorDefaultToNearest), ref info))
                    {
                        Rect work = info.Work;

                        // Leave a pixel free so an auto-hide taskbar can still be revealed.
                        if (work.Equals(info.Monitor))
                            work.Bottom -= 1;

                        *(Rect*)lParam = work;
                    }
                }

                return 0;
            }

            if (message == WmSize && wParam is 0 or 2)
            {
                var maximized = wParam == 2;

                if (_lastMaximized != maximized)
                {
                    _lastMaximized = maximized;

                    try
                    {
                        _stateChanged?.Invoke(maximized);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return CallWindowProc(_previous, window, message, wParam, lParam);
        }

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
