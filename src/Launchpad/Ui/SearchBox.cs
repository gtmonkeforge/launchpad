using GoomeSharp;

namespace Launchpad.Ui;

using Ui = GoomeSharp.Ui;

public static class SearchBox
{
    public static int Draw(Ui ui, string id, ref string text, double x, double y, double width, double height, string hint, bool slashChip)
    {
        ui.Pos(x, y);
        ui.Custom(id + "-box", width, height, ItemFlags.NoHit, out Item box);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, height, Palette.Radius, Palette.Bg, Palette.Line);
        Icons.Search(c, box.X + 16, box.Y + height / 2 - 1, Palette.Muted);

        var reserved = text.Length > 0 || slashChip ? 32 : 6;
        ui.Pos(x + 26, y);
        ui.Size(width - 26 - reserved, height);
        ui.PushColor(Style.InputBg, 0);
        ui.PushColor(Style.Hover, 0);
        ui.PushColor(Style.Border, 0);
        ui.PushColor(Style.Accent, 0);
        ui.PushColor(Style.Focus, 0);
        ui.PushColor(Style.Caret, Palette.Accent);
        ui.PushColor(Style.Select, 0x55DC5C3B);
        var result = ui.InputEx("##" + id, ref text, InputFlags.EnterReturns, hint);
        ui.Pop(7);

        c = ui.Canvas()!;

        if (ui.Focused)
            c.DrawRRectLine(box.X, box.Y, width, height, Palette.Radius, 1.5, Palette.Accent);

        if (text.Length > 0)
        {
            Item clear = Chrome.Hit(ui, id + "-clear", box.X + width - 30, box.Y + height / 2 - 11, 22, 22);
            c = ui.Canvas()!;

            if (clear.Hovered)
                c.DrawRRect(clear.X, clear.Y, 22, 22, 6, Palette.Hover);

            Icons.SmallClose(c, clear.X + 11, clear.Y + 11, clear.Hovered ? Palette.Text : Palette.Muted);

            if (clear.Clicked)
                text = "";
        }
        else if (slashChip)
        {
            Item chip = Chrome.Hit(ui, id + "-chip", box.X + width - 30, box.Y + height / 2 - 10, 20, 20);
            c = ui.Canvas()!;
            Chrome.Box(c, chip.X, chip.Y, 20, 20, 5, Palette.Surface2, Palette.LineStrong);
            Chrome.Text(c, Fonts.Mono, 12, chip.X + 10 - Fonts.Mono.Measure(12, "/") / 2, chip.Y + 10, "/", Palette.Muted);
        }

        return result;
    }
}
