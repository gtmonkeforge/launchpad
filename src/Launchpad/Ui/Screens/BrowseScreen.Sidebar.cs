using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class BrowseScreen
{
    private static readonly string[] Categories = ["", "Gameplay", "Cosmetic", "Utilities", "Accessibility", "Other"];
    private static readonly string[] LoaderNames = ["All loaders", "BepInEx 5", "BepInEx 6", "MelonLoader"];
    private static readonly string[] FormatNames = ["Any format", "DLL", "ZIP"];

    private const double WidgetHalf = 14;

    private double SidebarBottom;

    private void DrawSidebar(Ui ui, double top)
    {
        var y = top + 9;
        Title(ui, "Launchpad", y);
        var changed = Check(ui, "Installed in Gorilla Tag", ref _installed, y + 29);

        y += 69;
        Title(ui, "Category", y);
        y += 17;

        foreach (var category in Categories)
        {
            if (CategoryRow(ui, category, y))
            {
                _category = category;
                changed = true;
            }

            y += 34.6;
        }

        y += 29.4;
        Title(ui, "Capabilities", y);
        changed |= RadioGroup(ui, "capability", ["Auto-updating", "Extensions", "Networking", "Any"], [1, 2, 3, 0], ref _capability, y + 28);

        y += 151;
        Title(ui, "Price", y);
        changed |= RadioGroup(ui, "price", ["Any price", "Free", "Paid"], [0, 1, 2], ref _price, y + 28);

        y += 124;
        Title(ui, "Mod loader", y);
        changed |= Pick(ui, "loader", LoaderNames, ref _loader, y + 18);

        y += 77;
        Title(ui, "Format", y);
        changed |= Pick(ui, "format", FormatNames, ref _format, y + 18);
        y += 69;

        if (shell.SignedIn)
        {
            changed |= Check(ui, "Only my mods", ref _mineOnly, y + 9);
            y += 40;
        }

        if (ResetButton(ui, y))
            changed = true;

        SidebarBottom = y + 50;

        if (changed)
            Load(1);
    }

    private static void Title(Ui ui, string text, double centerY)
    {
        Item item = Chrome.Hit(ui, "side-title-" + text, SideMargin, centerY - 9, 200, 18);
        Chrome.BoldText(ui.Canvas()!, Fonts.Main, 13, item.X, item.Y + 9, text, Palette.Faint);
    }

    private bool CategoryRow(Ui ui, string category, double top)
    {
        Item item = Chrome.Hit(ui, "category-" + category, SideMargin, top, SidebarWidth, 34);
        Canvas c = ui.Canvas()!;
        var selected = _category == category;

        if (selected)
            c.DrawRRect(item.X, item.Y, SidebarWidth, 34, 8, Palette.Surface2);
        else if (item.Hovered)
            c.DrawRRect(item.X, item.Y, SidebarWidth, 34, 8, Palette.Surface);

        Icons.Category(c, category, item.X + 16, item.Y + 17, selected ? Palette.Accent : Palette.Muted);
        Chrome.Text(c, Fonts.Main, 15, item.X + 34, item.Y + 17, category.Length == 0 ? "All mods" : category, selected || item.Hovered ? Palette.Text : Palette.Muted);
        return item.Clicked && !selected;
    }

    private static bool Check(Ui ui, string label, ref bool value, double centerY)
    {
        ui.Pos(SideMargin, centerY - WidgetHalf);
        return ui.Checkbox(label, ref value);
    }

    private static bool RadioGroup(Ui ui, string id, string[] labels, int[] values, ref int selected, double firstCenter)
    {
        var changed = false;
        ui.PushId(id);

        for (var i = 0; i < labels.Length; i++)
        {
            ui.Pos(SideMargin, firstCenter + i * 27.7 - WidgetHalf);
            changed |= ui.Radio(labels[i], ref selected, values[i]);
        }

        ui.PopId();
        return changed;
    }

    private static bool Pick(Ui ui, string id, string[] names, ref int selected, double top)
    {
        ui.Pos(SideMargin, top);
        ui.Size(SidebarWidth, 39);
        return ui.Combo("##" + id, ref selected, names);
    }

    private bool ResetButton(Ui ui, double top)
    {
        Item item = Chrome.Hit(ui, "reset-filters", SideMargin, top, SidebarWidth, 32);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, item.X, item.Y, SidebarWidth, 32, 8, item.Hovered ? Palette.Surface : Palette.Bg, Palette.Line);
        var text = "Reset filters";
        Chrome.Text(c, Fonts.Main, 13.5, item.X + SidebarWidth / 2 - Fonts.Main.Measure(13.5, text) / 2, item.Y + 16, text, item.Hovered ? Palette.Text : Palette.Muted);

        if (!item.Clicked)
            return false;

        _category = "";
        _capability = _price = _loader = _format = 0;
        _installed = _mineOnly = false;
        _sort = 0;
        shell.Search = "";
        return true;
    }

    private void DrawToolbar(Ui ui, double left, double width, double top)
    {
        var countText = _total + (_total == 1 ? " mod" : " mods");
        var countWidth = Fonts.Mono.Measure(13, countText);
        var free = width - (260 + countWidth + 200 + 30);
        var inputWidth = Math.Max(260, 260 + free / 2);

        var text = shell.Search;
        ui.Pos(left, top);
        ui.Size(inputWidth, 41);
        ui.InputEx("##browse-search", ref text, InputFlags.None, "Search by name, author or GUID");
        shell.Search = text;

        Item count = Chrome.Hit(ui, "browse-count", left + inputWidth + 10, top, countWidth + 4, 41);
        Canvas c = ui.Canvas()!;
        var number = _total.ToString();
        var x = Chrome.BoldText(c, Fonts.Mono, 13, count.X, count.Y + 20.5, number, Palette.Text);
        Chrome.Text(c, Fonts.Mono, 13, count.X + x, count.Y + 20.5, countText[number.Length..], Palette.Muted);

        ui.Pos(left + width - 200, top);
        ui.Size(200, 41);

        if (ui.Combo("##browse-sort", ref _sort, SortNames))
            Load(1);
    }
}
