using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;
using Launchpad.Mods;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private const double ActionWidth = 190;
    private const double ActionsWidth = 260;

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
            return LockedPrimary();

        if (_working)
            return ("Working…", false, null);

        if (!GameValid)
            return ("Choose your game folder", true, () => _shell.Router.Push(new Route(Page.Game)));

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
            notes.Add((PurchaseOptions().Any(option => option.Monthly) ? "Buy it once or pick a monthly tier." : "Buy once to install every future update.", Palette.Muted));
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

    private sealed record SideButton(string Id, string Text, ButtonStyle Style, bool Enabled, Action Click, Action<Canvas, double, double, uint>? Glyph);

    private List<SideButton> SideButtons()
    {
        var buttons = new List<SideButton>
        {
            new("mod-share", "Share", ButtonStyle.Plain, true, CopyLink, Glyphs.Link),
        };

        if (_mod.Str("source_url") is { } source)
            buttons.Add(new("mod-source", "Source", ButtonStyle.Plain, true, () => _shell.Call("shell.open", new JsonObject { ["url"] = source }), Glyphs.Code));

        if (CanDonate)
            buttons.Add(new("mod-donate", "Donate", ButtonStyle.Plain, true, OpenDonate, null));

        if (_owner)
            buttons.Add(new("mod-manage", "Manage", ButtonStyle.Plain, true, OpenManage, Glyphs.Gear));

        if (_record != null && !Locked && _record.Loader == _shell.App.Settings.Loader.Id() && Primary().Label != "Remove")
            buttons.Add(new("mod-remove", _confirmRemove ? "Click again to remove" : "Remove", ButtonStyle.Danger, !_working, RemoveClick, Glyphs.Trash));

        return buttons;
    }

    private void CopyLink()
    {
        _shell.Window.Goome.Clipboard = new Uri(_shell.App.Forge.BaseUri, "/view/" + Uri.EscapeDataString(_guid)).ToString();
        _shell.Toast("Link copied.");
    }

    private static List<(SideButton Button, double Width, int Row, double Left)> SideLayout(List<SideButton> buttons)
    {
        var placed = new List<(SideButton, double, int, double)>();
        var used = 0.0;
        var row = 0;

        foreach (var button in buttons)
        {
            var width = Fonts.Main.Measure(13.5, button.Text) + (button.Glyph == null ? 30 : 46);

            if (used > 0 && used + width > ActionsWidth)
            {
                row++;
                used = 0;
            }

            placed.Add((button, width, row, ActionsWidth - used - width));
            used += width + 8;
        }

        return placed;
    }

    private double ActionsHeight() => 48 + (SideLayout(SideButtons()).Max(item => item.Row) + 1) * 40 - 8;

    private void DrawActions(Ui ui, double areaLeft, double y)
    {
        var (label, enabled, click) = Primary();
        var removing = label == "Remove";
        var text = removing && _confirmRemove ? "Click again to remove" : label;
        var style = removing ? ButtonStyle.Danger : ButtonStyle.Primary;

        if (Button(ui, "mod-primary", text, areaLeft + ActionsWidth - ActionWidth, y, ActionWidth, 40, style, enabled, removing ? Glyphs.Trash : null))
            click?.Invoke();

        foreach (var (button, width, row, left) in SideLayout(SideButtons()))
        {
            if (Button(ui, button.Id, button.Text, areaLeft + left, y + 48 + row * 40, width, 32, button.Style, button.Enabled, button.Glyph))
                button.Click();
        }
    }
}
