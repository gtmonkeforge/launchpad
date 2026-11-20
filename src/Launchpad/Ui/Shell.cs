using System.Text.Json;
using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.App;
using Launchpad.Infrastructure;

namespace Launchpad.Ui;

public sealed class Shell
{
    private readonly Bridge _bridge;

    public Shell(LaunchpadApp app, GoomeApp window)
    {
        App = app;
        Window = window;
        _bridge = new Bridge(app, _ => Task.FromResult<string?>(null));
        Images = new ImageCache(app.Forge, Invalidate);
        Notifications = new Notifications(this);
        Footer = new Footer(this);
        Router = new Router(this);
        app.Forge.SessionChanged += Invalidate;
        app.Mods.Changed += Invalidate;
        app.Event += OnAppEvent;
    }

    public LaunchpadApp App { get; }
    public GoomeApp Window { get; }
    public Router Router { get; }
    public ImageCache Images { get; }
    public Notifications Notifications { get; }
    public Footer Footer { get; }
    public string Search { get; set; } = "";
    public string? Progress { get; private set; }

    public JsonObject? User => App.Forge.User;
    public bool SignedIn => User != null;
    public bool IsAdmin => User.Bool("is_admin");

    public void Invalidate()
    {
        Window.Invalidate();
        Window.Goome.Push(new Event(EventType.None));
    }

    public void Toast(string text)
    {
        _pendingToast = text;
        Invalidate();
    }

    public void OpenForge(string path) => Call("shell.openForge", new JsonObject { ["path"] = path });

    public bool SigningIn { get; private set; }

    public void SignIn()
    {
        if (SigningIn)
            return;

        SigningIn = true;
        CallAsync("auth.signIn", []).ContinueWith(_ =>
        {
            SigningIn = false;
            Invalidate();
        });
    }

    public void CancelSignIn() => Call("auth.cancel", []);

    public void SignOut() => Call("auth.signOut", []);

    private void OnAppEvent(string name, object? data)
    {
        if (name != "progress")
            return;

        Progress = JsonSerializer.SerializeToNode(data).Str("message");
        Invalidate();
    }

    public void Call(string method, JsonObject args, Action<object?>? done = null, Action<string>? failed = null) => _ = CallAsync(method, args, done, failed);

    public Task<bool> CallAsync(string method, JsonObject args, Action<object?>? done = null, Action<string>? failed = null)
    {
        return Task.Run(async () =>
        {
            var worked = true;

            try
            {
                var result = await _bridge.DispatchAsync(method, args, CancellationToken.None);
                done?.Invoke(result);
            }
            catch (UserFacingException ex)
            {
                _pendingToast = ex.Message;
                worked = false;
                failed?.Invoke(ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _pendingToast = ex.Message;
                worked = false;
                failed?.Invoke(ex.Message);
            }

            Invalidate();
            return worked;
        });
    }

    private string? _pendingToast;

    public void ShowPendingToast()
    {
        if (_pendingToast == null)
            return;

        var text = _pendingToast;
        _pendingToast = null;
        Window.Ui.Toast(text);
    }
}
