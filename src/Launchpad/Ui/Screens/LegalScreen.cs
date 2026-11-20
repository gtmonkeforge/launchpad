using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed class LegalScreen : IScreen
{
    private const double MostWidth = 820;

    private readonly Shell _shell;
    private readonly bool _terms;
    private readonly (string Heading, string Text)[] _sections;
    private double _laidOutWidth;
    private List<(string Heading, List<string> Lines)> _lines = [];

    public LegalScreen(Shell shell, string? arg)
    {
        _shell = shell;
        _terms = arg != "privacy";
        _sections = _terms ? LegalText.Terms : LegalText.Privacy;
    }

    public void Reload()
    {
    }

    public void Draw(Ui ui, double width, double height)
    {
        PageFrame.Begin(ui, "legal", height);
        var column = PageFrame.ColumnWidth(width, MostWidth);
        var left = PageFrame.ColumnLeft(width, column);

        Item back = Chrome.Hit(ui, "legal-back", left, 28, 130, 24);
        Canvas c = ui.Canvas()!;
        Chrome.Text(c, Fonts.Main, 14, back.X, back.Y + 12, "< Back to browse", back.Hovered ? Palette.Text : Palette.Muted);

        if (back.Clicked)
            _shell.Router.Push(new Route(Page.Browse));

        PageFrame.Title(ui, "legal-title", left, 62, _terms ? "Terms of Service" : "Privacy Policy", null);
        ui.Pos(left, 104);
        ui.Custom("legal-updated", 400, 20, ItemFlags.NoHit, out Item updated);
        Chrome.Text(ui.Canvas()!, Fonts.Mono, 12.5, updated.X, updated.Y + 10, LegalText.Updated, Palette.Faint);

        var bottom = DrawPanel(ui, left, 140, column);
        PageFrame.End(ui, _shell, "legal", bottom, height, width);
    }

    private double DrawPanel(Ui ui, double left, double top, double width)
    {
        if (_laidOutWidth != width)
        {
            _lines = _sections.Select(section => (section.Heading, Chrome.Wrap(Fonts.Main, 14.5, section.Text, width - 56, 200))).ToList();
            _laidOutWidth = width;
        }

        var height = 28.0;

        foreach (var section in _lines)
            height += 56 + section.Lines.Count * 23;

        ui.Pos(left, top);
        ui.Custom("legal-panel", width, height, ItemFlags.NoHit, out Item panel);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, panel.X, panel.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        var y = panel.Y + 28.0;

        foreach (var section in _lines)
        {
            Chrome.BoldText(c, Fonts.Main, 17, panel.X + 28, y + 12, section.Heading, Palette.Text);
            y += 38;

            foreach (var line in section.Lines)
            {
                Chrome.Text(c, Fonts.Main, 14.5, panel.X + 28, y + 11.5, line, Palette.Muted);
                y += 23;
            }

            y += 18;
        }

        return top + height;
    }
}
