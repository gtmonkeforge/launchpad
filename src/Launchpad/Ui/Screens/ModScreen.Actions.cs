using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;
using Launchpad.Mods;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private const double ActionWidth = 190;

    private string[] ReleaseLoaders()
    {
        var list = (Current?["loaders"] as JsonArray)?.Select(item => item?.GetValue<string>() ?? "").ToArray();
        return list is { Length: > 0 } ? list : ["bepinex5"];
    }

    private (string Label, bool Enabled, Action? Click) Primary()
    {
        var loader = _shell.App.Settings.Loader;
        var version = Current.Str("version");

        if (Current == null)
            return ("No releases yet", false, null);

        if (Locked)
        {
            var price = _mod.Long("price_cents") > 0 ? "Buy · " + Format.Money(_mod.Long("price_cents"), _mod.Str("currency") ?? "usd") : "See tiers";
            return (price, true, () => _shell.OpenForge("/view/" + Uri.EscapeDataString(_guid)));
        }

        if (_working)
            return ("Working…", false, null);

        if (!GameValid)
            return ("Choose your game folder", true, () => _shell.Router.Push(new Route(Page.Settings)));

        if (_record != null && _record.Loader == loader.Id() && _record.Version != version)
            return ("Update to v" + version, true, Install);

        if (_record != null)
            return ("Remove", !_working, RemoveClick);

        if (!ReleaseLoaders().Contains(loader.Id()))
            return ("Install", false, null);

        return (_mod.Bool("uses_injector") ? "Install with injector" : "Install", true, Install);
    }

    private List<(string Text, uint Color)> Notes()
    {
        var notes = new List<(string, uint)>();
        var loader = _shell.App.Settings.Loader;

        if (_working && _shell.Progress != null)
            notes.Add((_shell.Progress, Palette.Muted));

        if (_result != null)
            notes.Add((_result, Palette.Good));

        if (_failure != null)
            notes.Add((_failure, Palette.Bad));

        if (Locked)
            notes.Add(("Buy this mod on monkeforge.org, then install it here. Buy once to install every future update.", Palette.Muted));
        else if (!GameValid)
            notes.Add(("Launchpad needs to know where Gorilla Tag is installed.", Palette.Muted));
        else if (_record != null && _record.Loader != loader.Id())
            notes.Add(($"Installed for {LoaderLabel(_record.Loader)}. Switch back in Launchpad settings to use it.", Palette.Muted));
        else if (_record == null && !ReleaseLoaders().Contains(loader.Id()))
            notes.Add(($"Not available for {loader.DisplayName()}. It supports {string.Join(", ", ReleaseLoaders().Select(LoaderLabel))}.", Palette.Muted));

        if (_mod.Bool("uses_injector") && GameValid && !Locked)
            notes.Add(("Delivered encrypted and loaded into the game from memory each time Gorilla Tag starts while Launchpad is open.", Palette.Muted));

        return notes;
    }

    private void RemoveClick()
    {
        if (_confirmRemove)
            Remove();
        else
            _confirmRemove = true;
    }

    private void DrawActions(Ui ui, double x, double y)
    {
        var (label, enabled, click) = Primary();
        var removing = label == "Remove";
        var text = removing && _confirmRemove ? "Click again to remove" : label;
        var style = removing ? ButtonStyle.Danger : ButtonStyle.Primary;

        if (Button(ui, "mod-primary", text, x, y, ActionWidth, 40, style, enabled, removing ? Glyphs.Trash : null))
            click?.Invoke();

        var source = _mod.Str("source_url");
        var shareWidth = Fonts.Main.Measure(13.5, "Share") + 46;
        var sourceWidth = Fonts.Main.Measure(13.5, "Source") + 46;
        var right = x + ActionWidth;
        var top = y + 48;
        var alsoRemove = _record != null && !Locked && !removing && _record.Loader == _shell.App.Settings.Loader.Id();

        if (source != null)
        {
            right -= sourceWidth;

            if (Button(ui, "mod-source", "Source", right, top, sourceWidth, 32, ButtonStyle.Plain, true, Glyphs.Code))
                _shell.Call("shell.open", new JsonObject { ["url"] = source });

            right -= 8;
        }

        right -= shareWidth;

        if (Button(ui, "mod-share", "Share", right, top, shareWidth, 32, ButtonStyle.Plain, true, Glyphs.Link))
        {
            _shell.Window.Goome.Clipboard = new Uri(_shell.App.Forge.BaseUri, "/view/" + Uri.EscapeDataString(_guid)).ToString();
            _shell.Toast("Link copied.");
        }

        if (alsoRemove && Button(ui, "mod-remove", _confirmRemove ? "Click again to remove" : "Remove", x, top + 40, ActionWidth, 32, ButtonStyle.Danger, !_working, Glyphs.Trash))
            RemoveClick();
    }
}
