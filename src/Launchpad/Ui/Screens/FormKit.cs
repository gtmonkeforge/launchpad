using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public static class FormKit
{
    public const double Left = 30;
    public const double RowHeight = 39;

    public static double Label(Ui ui, string text, double y)
    {
        Item item = Chrome.Hit(ui, "form-label-" + text + y, Left, y, 600, 18);
        Chrome.BoldText(ui.Canvas()!, Fonts.Main, 13, item.X, item.Y + 9, text, Palette.Faint);
        return y + 22;
    }

    public static double Hint(Ui ui, string text, double y, double width)
    {
        var lines = Chrome.Wrap(Fonts.Main, 13, text, width, 4);

        for (var i = 0; i < lines.Count; i++)
        {
            Item line = Chrome.Hit(ui, "form-hint-" + y + "-" + i, Left, y + i * 19, width, 19);
            Chrome.Text(ui.Canvas()!, Fonts.Main, 13, line.X, line.Y + 9.5, lines[i], Palette.Faint);
        }

        return y + lines.Count * 19 + 10;
    }

    public static double Field(Ui ui, string id, string label, ref string value, double y, double width, string placeholder, string? hint = null, int maxBytes = 256)
    {
        y = Label(ui, label, y);
        ui.Pos(Left, y);
        ui.Size(width, RowHeight);
        ui.InputEx("##form-" + id, ref value, InputFlags.None, placeholder, maxBytes);
        y += RowHeight + 8;
        return hint == null ? y + 8 : Hint(ui, hint, y, width) + 2;
    }

    public static double TextArea(Ui ui, string id, string label, ref string value, double y, double width, double height, string? hint = null, int maxBytes = 10000)
    {
        y = Label(ui, label, y);
        ui.Pos(Left, y);
        ui.Size(width, height);
        ui.TextEdit("##form-" + id, ref value, height, EditFlags.Wrap, maxBytes);
        y += height + 8;
        return hint == null ? y + 8 : Hint(ui, hint, y, width) + 2;
    }

    public static double Pair(Ui ui, string id, string label, ref int value, string[] items, double y, double width)
    {
        y = Label(ui, label, y);
        ui.Pos(Left, y);
        ui.Size(width, RowHeight);
        ui.Combo("##form-" + id, ref value, items);
        return y + RowHeight + 16;
    }

    public static double Error(Ui ui, string? message, double y, double width)
    {
        if (message == null)
            return y;

        var lines = Chrome.Wrap(Fonts.Main, 14, message, width, 3);

        for (var i = 0; i < lines.Count; i++)
        {
            Item line = Chrome.Hit(ui, "form-error-" + i, Left, y + i * 21, width, 21);
            Chrome.Text(ui.Canvas()!, Fonts.Main, 14, line.X, line.Y + 10.5, lines[i], Palette.Bad);
        }

        return y + lines.Count * 21 + 8;
    }

    public static bool ActionButton(Ui ui, string id, string text, double x, double y, bool primary, out double width, bool enabled = true)
    {
        return Widgets.Button(ui, id, x, y, text, primary ? ButtonKind.Primary : ButtonKind.Subtle, out width, enabled, 36);
    }

    public static bool ActionButton(Ui ui, string id, string text, double x, double y, bool primary = false) => ActionButton(ui, id, text, x, y, primary, out _);

    public static void Heading(Ui ui, string id, string text, double y)
    {
        Item item = Chrome.Hit(ui, id, Left, y, 600, 26);
        Chrome.BoldText(ui.Canvas()!, Fonts.Main, 17, item.X, item.Y + 13, text, Palette.Text);
    }
}
