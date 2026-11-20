using GoomeSharp;

namespace Launchpad.Ui;

using Ui = GoomeSharp.Ui;

public sealed class FrameLoop(GoomeApp window, Action<Event> onEvent, Action<Ui> draw)
{
    private volatile bool _running;
    private volatile int _frames = 2;

    public void Wake() => _frames = 2;

    public void Stop() => _running = false;

    public void Run()
    {
        var pace = new Pacer();
        var goome = window.Goome;
        var ui = window.Ui;
        _running = true;

        while (_running)
        {
            var wait = _frames > 0 || ui.Animating ? pace.Pace(60) : 60;

            while (goome.Poll(out Event ev, wait))
            {
                wait = 0;
                onEvent(ev);

                if (ev.Type == EventType.Quit || (ev.Type == EventType.Close && (window.OnClose?.Invoke() ?? true)))
                {
                    _running = false;
                    break;
                }

                if (ev.Type == EventType.Close)
                    continue;

                ui.Feed(ev);
                Wake();
            }

            if (!_running || (_frames == 0 && !ui.Animating))
                continue;

            if (_frames > 0)
                _frames--;

            if (ui.Begin(0) == null)
                continue;

            ui.App(window.Title, window.Flags);
            draw(ui);
            ui.AppEnd();
            ui.End();
        }
    }
}
