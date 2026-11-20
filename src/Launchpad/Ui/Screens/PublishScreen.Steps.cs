using GoomeSharp;
using static Launchpad.Ui.Screens.FormKit;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class PublishScreen
{
    private double DrawStep(Ui ui, double width, double y)
    {
        y = _step switch
        {
            0 => DrawIdentity(ui, width, y),
            1 => _listing.Draw(ui, width, y),
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

    private double DrawRelease(Ui ui, double width, double y)
    {
        y = _release.Draw(ui, width, y, false);
        y = Hint(ui, "Set pricing and payouts from the Manage page once the mod is published.", y, width);
        Item publishing = Chrome.Hit(ui, "publish-as", SideMargin, y + 4, width, 20);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 13, publishing.X, publishing.Y + 10, "Publishing as " + _authors![_author].Name, Palette.Muted);
        return y + 36;
    }

    private double DrawNav(Ui ui, double width, double y)
    {
        if (_step > 0 && ActionButton(ui, "publish-back", "Back", SideMargin, y))
            _step--;

        if (_step < 2 && ActionButton(ui, "publish-next", "Continue", SideMargin + width - Widgets.ButtonWidth("Continue"), y, true))
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
            else if (ActionButton(ui, "publish-send", "Publish mod", SideMargin + width - Widgets.ButtonWidth("Publish mod"), y, true))
                _ = Publish();
        }

        return y + 40;
    }
}
