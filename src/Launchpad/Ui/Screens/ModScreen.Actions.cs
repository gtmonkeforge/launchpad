using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;
using Launchpad.Mods;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
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
        var buttons = new List<SideButton>();

        if (CanDonate)
            buttons.Add(new("mod-donate", "Donate", ButtonStyle.Plain, true, OpenDonate, Glyphs.Heart));

        buttons.Add(new("mod-share", "Share", ButtonStyle.Plain, true, CopyLink, Glyphs.Link));

        if (_mod.Str("source_url") is { } source)
            buttons.Add(new("mod-source", "Source", ButtonStyle.Plain, true, () => _shell.Call("shell.open", new JsonObject { ["url"] = source }), Glyphs.Code));

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
        var rows = new List<List<(SideButton, double)>> { new() };

        foreach (var button in buttons)
        {
            var width = Fonts.Main.Measure(13.5, button.Text) + 46;
            var used = rows[^1].Sum(item => item.Item2 + 8);

            if (used > 0 && used + width > ActionsWidth)
                rows.Add(new());

            rows[^1].Add((button, width));
        }

        var placed = new List<(SideButton, double, int, double)>();

        for (var row = 0; row < rows.Count; row++)
        {
            var left = ActionsWidth - rows[row].Sum(item => item.Item2 + 8) + 8;

            foreach (var (button, width) in rows[row])
            {
                placed.Add((button, width, row, left));
                left += width + 8;
            }
        }

        return placed;
    }

    private double ActionsHeight() => 80 + SideLayout(SideButtons()).Max(item => item.Row) * 38;

    private static Action<Canvas, double, double, uint>? PrimaryGlyph(string label) => label switch
    {
        "Choose your game folder" => Glyphs.Game,
        "Sign in to buy" => Glyphs.User,
        "Remove" => Glyphs.Trash,
        "Install" or "Install with injector" => Icons.Download,
        _ when label.StartsWith("Update", StringComparison.Ordinal) => Icons.Download,
        _ => null,
    };

    private void DrawActions(Ui ui, double areaLeft, double y)
    {
        var (label, enabled, click) = Primary();
        var removing = label == "Remove";
        var text = removing && _confirmRemove ? "Click again to remove" : label;
        var style = removing ? ButtonStyle.Danger : ButtonStyle.Primary;
        var glyph = PrimaryGlyph(label);
        var width = Fonts.Main.Measure(15, text) + (glyph == null ? 40 : 66);

        if (Button(ui, "mod-primary", text, areaLeft + ActionsWidth - width, y, width, 42, style, enabled, glyph))
            click?.Invoke();

        foreach (var (button, buttonWidth, row, left) in SideLayout(SideButtons()))
        {
            if (Button(ui, button.Id, button.Text, areaLeft + left, y + 50 + row * 38, buttonWidth, 30, button.Style, button.Enabled, button.Glyph))
                button.Click();
        }
    }
}
