using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class BrowseScreen
{
    private static readonly string[] Categories = ["", "Gameplay", "Cosmetic", "Utilities", "Accessibility", "Other"];
    private static readonly string[] LoaderNames = ["All loaders", "BepInEx 5", "BepInEx 6", "MelonLoader"];
    private static readonly string[] CapabilityNames = ["Auto-updating", "Extensions", "Networking"];
    private static readonly string[] FormatNames = ["Any format", "DLL", "ZIP"];

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
        Chrome.BoldText(ui.Canvas()!, Fonts.Main, 12.5, item.X, item.Y + 9, text, Palette.Faint);
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
        Chrome.Text(c, Fonts.Main, 13.5, item.X + 34, item.Y + 17, category.Length == 0 ? "All mods" : category, selected || item.Hovered ? Palette.Text : Palette.Muted);
        return item.Clicked && !selected;
    }

    private static bool Check(Ui ui, string label, ref bool value, double centerY)
    {
        if (!Choice(ui, "check-" + label, label, value, false, centerY))
            return false;

        value = !value;
        return true;
    }

    private static bool RadioGroup(Ui ui, string id, string[] labels, int[] values, ref int selected, double firstCenter)
    {
        var changed = false;

        for (var i = 0; i < labels.Length; i++)
        {
            if (Choice(ui, id + "-" + labels[i], labels[i], selected == values[i], true, firstCenter + i * 27.7) && selected != values[i])
            {
                selected = values[i];
                changed = true;
            }
        }

        return changed;
    }

    private static bool Choice(Ui ui, string id, string label, bool on, bool round, double centerY)
    {
        Item item = Chrome.Hit(ui, id, SideMargin - 4, centerY - 12, SidebarWidth + 4, 24);
        Canvas c = ui.Canvas()!;
        var x = item.X + 4;
        var y = item.Y + 12;
        var ring = item.Hovered ? 0xFF5A5E66 : Palette.LineStrong;

        if (round)
        {
            c.DrawCircle(x + 8, y, 8, on ? Palette.Accent : ring);
            c.DrawCircle(x + 8, y, on ? 8 : 6.5, on ? Palette.Accent : 0xFFF4F4F6);

            if (on)
                c.DrawCircle(x + 8, y, 2.6, 0xFFFFFFFF);
        }
        else
        {
            c.DrawRRect(x, y - 8, 16, 16, 3.5, on ? Palette.Accent : ring);
            c.DrawRRect(x + (on ? 0 : 1.5), y - 8 + (on ? 0 : 1.5), on ? 16 : 13, on ? 16 : 13, on ? 3.5 : 2.5, on ? Palette.Accent : 0xFFF4F4F6);

            if (on)
                Icons.Check(c, x + 8, y, 0xFFFFFFFF);
        }

        Chrome.Text(c, Fonts.Main, 13.5, x + 26, y, label, item.Hovered || on ? Palette.Text : Palette.Muted);
        return item.Clicked;
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
        var chips = ActiveFilters();
        var chipsWidth = chips.Sum(chip => ChipWidth(chip.Label) + 6);
        var free = width - (260 + countWidth + 200 + 30 + chipsWidth);
        var inputWidth = Math.Max(260, 260 + free / 2);

        var text = shell.Search;
        SearchBox.Draw(ui, "browse-search", ref text, left, top, inputWidth, 41, "Search by name, author or GUID", false);
        shell.Search = text;

        Item count = Chrome.Hit(ui, "browse-count", left + inputWidth + 10, top, countWidth + 4, 41);
        Canvas c = ui.Canvas()!;
        var number = _total.ToString();
        var x = Chrome.BoldText(c, Fonts.Mono, 13, count.X, count.Y + 20.5, number, Palette.Text);
        Chrome.Text(c, Fonts.Mono, 13, count.X + x, count.Y + 20.5, countText[number.Length..], Palette.Muted);

        var chipX = left + inputWidth + 10 + countWidth + 14;

        foreach (var chip in chips)
        {
            if (Chip(ui, chip.Label, chipX, top + 9.5))
            {
                chip.Clear();
                Load(1);
            }

            chipX += ChipWidth(chip.Label) + 6;
        }

        ui.Pos(left + width - 200, top);
        ui.Size(200, 41);

        if (ui.Combo("##browse-sort", ref _sort, SortNames))
            Load(1);
    }

    private List<(string Label, Action Clear)> ActiveFilters()
    {
        var chips = new List<(string, Action)>();
        var term = shell.Search.Trim();

        if (term.Length > 0)
            chips.Add(("\u201c" + Chrome.Clip(Fonts.Main, 12.5, term, 120) + "\u201d", () => shell.Search = ""));

        if (_category.Length > 0)
            chips.Add((_category, () => _category = ""));

        if (_capability > 0)
            chips.Add((CapabilityNames[_capability - 1], () => _capability = 0));

        if (_loader > 0)
            chips.Add((LoaderNames[_loader], () => _loader = 0));

        if (_format > 0)
            chips.Add((FormatNames[_format], () => _format = 0));

        if (_price > 0)
            chips.Add((_price == 1 ? "Free" : "Paid", () => _price = 0));

        return chips;
    }

    private static double ChipWidth(string label) => Fonts.Main.Measure(12.5, label) + 38;

    private static bool Chip(Ui ui, string label, double x, double y)
    {
        var width = ChipWidth(label);
        Item item = Chrome.Hit(ui, "chip-" + label, x, y, width, 22);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, item.X, item.Y, width, 22, 6, item.Hovered ? 0xFF35231F : 0xFF2B1D1A, Palette.AccentLine);
        Chrome.Text(c, Fonts.Main, 12.5, item.X + 10, item.Y + 11, label, Palette.Text);
        Icons.SmallClose(c, item.X + width - 13, item.Y + 11, Palette.Muted);
        return item.Clicked;
    }
}
