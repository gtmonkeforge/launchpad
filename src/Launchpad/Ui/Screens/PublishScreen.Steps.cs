using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class PublishScreen
{
    private double DrawStep(Ui ui, double width, double y)
    {
        y = _step switch
        {
            0 => DrawIdentity(ui, width, y),
            1 => DrawPage(ui, width, y),
            _ => DrawRelease(ui, width, y),
        };

        if (_error != null)
        {
            Item error = Chrome.Hit(ui, "publish-error", SideMargin, y, width, 22);
            Chrome.Text(ui.Canvas()!, Fonts.Main, 14, error.X, error.Y + 11, Chrome.Clip(Fonts.Main, 14, _error, width), Palette.Bad);
            y += 30;
        }

        return DrawNav(ui, width, y + 8);
    }

    private double DrawIdentity(Ui ui, double width, double y)
    {
        y = Field(ui, "name", "Mod name", ref _name, y, width, "Your mod's display name", maxBytes: 400);
        y = Field(ui, "guid", "GUID (permanent)", ref _guid, y, width, "com.yourname.yourmod", "Every release of this mod uses the same GUID. Staff check it when they review the mod.", 800);

        if (_authors!.Count > 1)
        {
            y = Label(ui, "Publish as", y);
            ui.Pos(SideMargin, y);
            ui.Size(width, 39);
            ui.Combo("##publish-author", ref _author, _authors.Select(a => a.Name + (a.Id == shell.App.Forge.UserId ? " (you)" : " · group")).ToArray());
            y = Hint(ui, "Mods published as a group can be updated by every member of the group.", y + 45, width);
        }

        if (_authors[_author].Verified)
            y = Field(ui, "slug", "Custom URL (verified benefit)", ref _slug, y, width, "your-mod", null, 160);

        y = Label(ui, "Category", y);
        ui.Pos(SideMargin, y);
        ui.Size(width / 2 - 6, 39);
        ui.Combo("##publish-category", ref _category, Categories);
        ui.Pos(SideMargin + width / 2 + 6, y);
        ui.Size(width / 2 - 6, 39);
        ui.Combo("##publish-visibility", ref _visibility, Visibilities);
        Item note = Chrome.Hit(ui, "publish-visibility-label", SideMargin + width / 2 + 6, y - 22, width / 2, 18);
        Chrome.BoldText(ui.Canvas()!, Fonts.Main, 13, note.X, note.Y + 9, "Visibility", Palette.Faint);
        y += 55;

        y = Field(ui, "source", "Source code (optional)", ref _source, y, width, "https://github.com/you/your-mod", null, 2000);

        ui.Pos(SideMargin, y);
        ui.Checkbox("Install through the injector", ref _injector);
        return Hint(ui, "For closed-source mods. Files are encrypted and can only be installed through Launchpad, never downloaded. Each loader takes one DLL. Injector mods are always reviewed by a person. This can't be undone.", y + 36, width) + 8;
    }

    private double DrawPage(Ui ui, double width, double y)
    {
        y = Field(ui, "description", "Description (Markdown)", ref _description, y, width, "What does your mod do? Include install steps and anything players should know.", "One line here; use \\n for line breaks. You can edit the full text on monkeforge.org after publishing.", 30000);
        y = Field(ui, "logo", "Logo (optional, square, 5 MB)", ref _logoPath, y, width, "Path to a PNG, JPG or WebP file", null, 1000);
        y = Field(ui, "banner", "Banner (optional, wide, 5 MB)", ref _bannerPath, y, width, "Path to a PNG, JPG or WebP file", "Screenshots can be added on monkeforge.org.", 1000);

        y = Label(ui, "Capabilities", y);

        for (var i = 0; i < 3; i++)
        {
            ui.Pos(SideMargin + i * 170, y);
            ui.Checkbox(CapabilityNames[i], ref _capabilities[i]);
        }

        y = Hint(ui, "Declare automatic updates, extensions and networking so players know what to expect.", y + 36, width);
        y = Label(ui, "Links (up to three, besides source code)", y + 4);

        for (var i = 0; i < 3; i++)
        {
            ui.Pos(SideMargin, y);
            ui.Size(width * 0.35, 39);
            ui.InputEx("##publish-link-label-" + i, ref _linkLabels[i], InputFlags.None, "Discord server", 160);
            ui.Pos(SideMargin + width * 0.35 + 8, y);
            ui.Size(width * 0.65 - 8, 39);
            ui.InputEx("##publish-link-url-" + i, ref _linkUrls[i], InputFlags.None, "https://", 2000);
            y += 47;
        }

        return y + 8;
    }

    private double DrawRelease(Ui ui, double width, double y)
    {
        y = Hint(ui, "Releases are immutable: versions, files and changelogs can't be edited after publishing. Ship a new version to update downloads.", y, width);
        y = Field(ui, "version", "Version", ref _version, y + 6, width, "1.0.0", "The version players see, such as 1.0.0.", 128);
        y = Label(ui, "Downloads (one DLL, one ZIP, or both per loader, 50 MB each)", y);

        for (var i = 0; i < 3; i++)
        {
            Item name = Chrome.Hit(ui, "publish-loader-" + i, SideMargin, y, 120, 39);
            Chrome.BoldText(ui.Canvas()!, Fonts.Main, 14, name.X, name.Y + 19.5, LoaderNames[i], Palette.Text);
            ui.Pos(SideMargin + 120, y);
            ui.Size((width - 132) / 2, 39);
            ui.InputEx("##publish-dll-" + i, ref _dllPaths[i], InputFlags.None, "Path to a .dll", 1000);
            ui.Pos(SideMargin + 120 + (width - 132) / 2 + 12, y);
            ui.Size((width - 132) / 2, 39);
            ui.InputEx("##publish-zip-" + i, ref _zipPaths[i], InputFlags.None, "Path to a .zip", 1000);
            y += 47;
        }

        y = Field(ui, "notes", "Changelog (optional)", ref _notes, y + 8, width, "What's new in this release?", null, 10000);
        y = Hint(ui, "Pricing, payouts and screenshots are set up on monkeforge.org after publishing.", y, width);
        Item publishing = Chrome.Hit(ui, "publish-as", SideMargin, y + 4, width, 20);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 13, publishing.X, publishing.Y + 10, "Publishing as " + _authors![_author].Name, Palette.Muted);

        if (ActionButton(ui, "publish-open-site", "Open on monkeforge.org", SideMargin + width - 190, y, 190, false))
            shell.OpenForge("/upload");

        return y + 36;
    }

    private double DrawNav(Ui ui, double width, double y)
    {
        if (_step > 0 && ActionButton(ui, "publish-back", "Back", SideMargin, y, 100, false))
            _step--;

        if (_step < 2 && ActionButton(ui, "publish-next", "Continue", SideMargin + width - 130, y, 130, true))
        {
            _error = null;
            _step++;
        }

        if (_step == 2)
        {
            if (_busy)
            {
                ui.Pos(SideMargin + width - 130, y + 6);
                ui.Spinner(24);
            }
            else if (ActionButton(ui, "publish-send", "Publish mod", SideMargin + width - 130, y, 130, true))
                _ = Publish();
        }

        return y + 40;
    }

    private static double Label(Ui ui, string text, double y)
    {
        Item item = Chrome.Hit(ui, "publish-label-" + text, SideMargin, y, 600, 18);
        Chrome.BoldText(ui.Canvas()!, Fonts.Main, 13, item.X, item.Y + 9, text, Palette.Faint);
        return y + 22;
    }

    private static double Hint(Ui ui, string text, double y, double width)
    {
        var lines = Chrome.Wrap(Fonts.Main, 13, text, width, 4);

        for (var i = 0; i < lines.Count; i++)
        {
            Item line = Chrome.Hit(ui, "publish-hint-" + y + "-" + i, SideMargin, y + i * 19, width, 19);
            Chrome.Text(ui.Canvas()!, Fonts.Main, 13, line.X, line.Y + 9.5, lines[i], Palette.Faint);
        }

        return y + lines.Count * 19 + 10;
    }

    private static double Field(Ui ui, string id, string label, ref string value, double y, double width, string placeholder, string? hint = null, int maxBytes = 256)
    {
        y = Label(ui, label, y);
        ui.Pos(SideMargin, y);
        ui.Size(width, 39);
        ui.InputEx("##publish-" + id, ref value, InputFlags.None, placeholder, maxBytes);
        y += 39 + 8;
        return hint == null ? y + 8 : Hint(ui, hint, y, width) + 2;
    }

    private static bool ActionButton(Ui ui, string id, string text, double x, double y, double width, bool primary)
    {
        Item item = Chrome.Hit(ui, id, x, y, width, 36);
        Canvas c = ui.Canvas()!;

        if (primary)
            c.DrawRRect(item.X, item.Y, width, 36, Palette.Radius, item.Hovered ? Palette.AccentHot : Palette.Accent);
        else
            Chrome.Box(c, item.X, item.Y, width, 36, Palette.Radius, item.Hovered ? Palette.Hover : Palette.Surface2, Palette.LineStrong);

        var color = primary ? Palette.AccentInk : Palette.Text;
        Chrome.BoldText(c, Fonts.Main, 14.5, item.X + width / 2 - Fonts.Main.Measure(14.5, text) / 2, item.Y + 18, text, color);
        return item.Clicked;
    }
}
