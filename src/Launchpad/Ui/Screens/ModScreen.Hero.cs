using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;
using Launchpad.Mods;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private const double HeroPad = 22;
    private const double ActionWidth = 224;

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

        if (_record != null)
        {
            if (_record.Loader != loader.Id())
                return ("Installed for " + LoaderLabel(_record.Loader), false, null);

            if (_record.Version != version)
                return ("Update to v" + version, true, Install);

            return ("Installed · v" + _record.Version, false, null);
        }

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

    private double DrawHero(Ui ui, double width, double y)
    {
        var badges = (_mod!["badges"] as JsonArray)?.Select(item => item?.GetValue<string>() ?? "").ToArray() ?? [];
        var height = badges.Length > 0 ? 146.0 : 122.0;
        Item card = Area(ui, "mod-hero", SideMargin, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        DrawIcon(c, card.X + HeroPad, card.Y + HeroPad);

        var textLeft = card.X + HeroPad + 86 + 20;
        var textWidth = width - (textLeft - card.X) - ActionWidth - HeroPad - 16;
        Chrome.BoldText(c, Fonts.Main, 26, textLeft, card.Y + 38, Chrome.Clip(Fonts.Main, 26, _mod.Str("name") ?? "", textWidth), Palette.Text);
        DrawMeta(c, textLeft, card.Y + 74, textWidth);

        if (badges.Length > 0)
            DrawBadgeRow(c, badges, textLeft, card.Y + 94);

        DrawActions(ui, SideMargin + width - HeroPad - ActionWidth, y + HeroPad);
        return y + height;
    }

    private void DrawIcon(Canvas c, double x, double y)
    {
        Bitmap? logo = _shell.Images.Get(_mod.Str("logo"));

        if (logo != null)
        {
            c.DrawBitmap(logo, x, y, 86, 86, 16);
            return;
        }

        Chrome.Box(c, x, y, 86, 86, 16, Palette.Surface2, Palette.Line);
        var letters = Format.Initials(_mod.Str("name"));
        Chrome.BoldText(c, Fonts.Main, 28, x + 43 - Fonts.Main.Measure(28, letters) / 2, y + 43, letters, Palette.Muted);
    }

    private void DrawMeta(Canvas c, double x, double centerY, double maxWidth)
    {
        JsonNode? author = _mod!["author"];
        var right = x + maxWidth;
        x += Chrome.Text(c, Fonts.Main, 14, x, centerY, "by " + author.Str("name"), Palette.Muted) + 10;

        if (author.Bool("is_admin"))
        {
            Icons.Shield(c, x + 6, centerY, Palette.Admin);
            x += 16 + Chrome.BoldText(c, Fonts.Main, 12.5, x + 16, centerY, "Admin", Palette.Admin) + 10;
        }

        if (author.Bool("is_verified"))
        {
            Icons.Check(c, x + 6, centerY, Palette.Good);
            x += 16 + Chrome.BoldText(c, Fonts.Main, 12.5, x + 16, centerY, "Verified", Palette.Good) + 10;
        }

        var pills = new List<(string, uint)> { (_mod.Str("category") ?? "", Palette.Muted) };

        if (_mod["channel"].Str("name") is { } channel && channel != "stable")
            pills.Add((channel, Palette.Accent));

        if (_mod.Str("visibility") is { } visibility && visibility != "public")
            pills.Add((visibility == "private" ? "Private" : "Code-only", Palette.Muted));

        if (_mod.Bool("gated"))
            pills.Add((_mod.Bool("owned") ? "You own this mod" : _mod.Long("price_cents") > 0 ? Format.Money(_mod.Long("price_cents"), _mod.Str("currency") ?? "usd") : "From " + Format.Money(_mod.Long("min_tier_cents"), _mod.Str("currency") ?? "usd") + "/mo", Palette.Accent));

        if (_mod.Bool("uses_injector"))
            pills.Add(("Injector", Palette.Muted));

        foreach (var (text, color) in pills)
        {
            if (x + Fonts.Main.Measure(12.5, text) + 18 > right)
                break;

            x += Pill(c, x, centerY, text, color) + 6;
        }
    }

    private static void DrawBadgeRow(Canvas c, string[] badges, double x, double top)
    {
        foreach (var badge in badges)
        {
            var chip = Fonts.Main.Measure(13, badge) + 34;
            c.DrawRRect(x, top, chip, 23, 7, 0x4DE8C886);
            c.DrawRRect(x + 1, top + 1, chip - 2, 21, 6, Palette.Surface);
            c.DrawPoly([x + 14, top + 6.5, x + 15.4, top + 10, x + 19, top + 11.5, x + 15.4, top + 13, x + 14, top + 16.5, x + 12.6, top + 13, x + 9, top + 11.5, x + 12.6, top + 10], 0xFFE8C886);
            Chrome.Text(c, Fonts.Main, 13, x + 24, top + 11.5, badge, 0xFFE8C886);
            x += chip + 6;
        }
    }

    private void DrawActions(Ui ui, double x, double y)
    {
        var (label, enabled, click) = Primary();

        if (Button(ui, "mod-primary", label, x, y, ActionWidth, 46, ButtonStyle.Primary, enabled))
            click?.Invoke();

        var second = y + 56;
        var source = _mod.Str("source_url");
        var showRemove = _record != null && !Locked && _record.Loader == _shell.App.Settings.Loader.Id();
        var buttonWidth = showRemove && source != null ? ActionWidth / 2 - 4 : ActionWidth;

        if (showRemove)
        {
            var text = _confirmRemove ? "Click again to remove" : "Remove";

            if (Button(ui, "mod-remove", text, x, second, buttonWidth, 34, ButtonStyle.Danger, !_working))
            {
                if (_confirmRemove)
                    Remove();
                else
                    _confirmRemove = true;
            }

            x += buttonWidth + 8;
        }

        if (source != null && Button(ui, "mod-source", "Source", x, second, buttonWidth, 34, ButtonStyle.Plain))
            _shell.Call("shell.open", new JsonObject { ["url"] = source });
    }
}
