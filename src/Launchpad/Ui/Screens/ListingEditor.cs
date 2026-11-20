using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ListingEditor(Shell shell, FilePicker picker)
{
    private static readonly string[] CapabilityIds = ["auto_update", "extensions", "internet"];
    private static readonly string[] CapabilityNames = ["Auto-updating", "Extensions", "Networking"];
    private static readonly (string Label, string Before, string Sample, string After, bool OwnLine)[] Tools =
    [
        ("Bold", "**", "bold text", "**", false),
        ("Italic", "*", "italic text", "*", false),
        ("Heading", "## ", "Heading", "", true),
        ("List", "- ", "item", "", true),
        ("Link", "[", "link text", "](https://)", false),
        ("Code", "`", "code", "`", false),
    ];

    private bool _hidePreview;
    private MarkdownLayout? _layout;
    private string _layoutText = "";
    private double _layoutWidth;

    public string Description = "";
    public readonly bool[] Capabilities = [false, false, false];
    public readonly string[] LinkLabels = ["", "", ""];
    public readonly string[] LinkUrls = ["", "", ""];
    public double OriginX;
    public double OriginY;

    public double Draw(Ui ui, double width, double y)
    {
        y = DrawDescription(ui, width, y);
        y = DrawImages(ui, width, y);
        return DrawExtras(ui, width, y);
    }

    private double DrawDescription(Ui ui, double width, double y)
    {
        y = FormKit.Label(ui, "Description (Markdown)", y);
        var x = FormKit.Left;

        foreach (var tool in Tools)
        {
            if (Widgets.Button(ui, "listing-tool-" + tool.Label, x, y, tool.Label, ButtonKind.Subtle, out var toolWidth, true, 28))
                Insert(tool.Before + tool.Sample + tool.After, tool.OwnLine);

            x += toolWidth + 6;
        }

        var previewText = _hidePreview ? "Show preview" : "Hide preview";

        if (Widgets.Button(ui, "listing-preview-toggle", FormKit.Left + width - Widgets.ButtonWidth(previewText), y, previewText, ButtonKind.Subtle, out _, true, 28))
            _hidePreview = !_hidePreview;

        y += 36;
        ui.Pos(FormKit.Left, y);
        ui.Size(width, 240);
        ui.TextEdit("##listing-description", ref Description, 240, EditFlags.Wrap, 30000);
        y += 248;

        if (!_hidePreview)
            y = DrawPreview(ui, width, y + 6);

        return FormKit.Hint(ui, "Markdown works here. Use the buttons above to add formatting at the end of your text.", y + 4, width);
    }

    private double DrawPreview(Ui ui, double width, double y)
    {
        if (_layout == null || _layoutText != Description || _layoutWidth != width)
        {
            _layout = Markdown.Layout(Description.Length == 0 ? "Your description appears here." : Description, width - 44);
            _layoutText = Description;
            _layoutWidth = width;
        }

        y = FormKit.Label(ui, "Preview", y);
        var height = Math.Max(60, _layout.Height + 44);
        Item box = Chrome.Hit(ui, "listing-preview-box", FormKit.Left, y, width, height);
        Chrome.Box(ui.Canvas()!, box.X, box.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Markdown.Draw(ui, _layout, "listing-md-", FormKit.Left + 22, y + 22, OriginX, OriginY, url => shell.OpenForge(url));
        return y + height;
    }

    private void Insert(string markup, bool ownLine)
    {
        if (ownLine && Description.Length > 0 && !Description.EndsWith('\n'))
            Description += "\n";
        else if (!ownLine && Description.Length > 0 && !char.IsWhiteSpace(Description[^1]))
            Description += " ";

        Description += markup;
    }

    private double DrawExtras(Ui ui, double width, double y)
    {
        y = FormKit.Label(ui, "Capabilities", y);

        for (var i = 0; i < 3; i++)
        {
            ui.Pos(FormKit.Left + i * 170, y);
            ui.Checkbox(CapabilityNames[i], ref Capabilities[i]);
        }

        y = FormKit.Hint(ui, "Declare automatic updates, extensions and networking so players know what to expect.", y + 36, width);
        y = FormKit.Label(ui, "Links (up to three, besides source code)", y + 4);

        for (var i = 0; i < 3; i++)
        {
            ui.Pos(FormKit.Left, y);
            ui.Size(width * 0.35, FormKit.RowHeight);
            ui.InputEx("##listing-link-label-" + i, ref LinkLabels[i], InputFlags.None, "Discord server", 160);
            ui.Pos(FormKit.Left + width * 0.35 + 8, y);
            ui.Size(width * 0.65 - 8, FormKit.RowHeight);
            ui.InputEx("##listing-link-url-" + i, ref LinkUrls[i], InputFlags.None, "https://", 2000);
            y += 47;
        }

        return y + 8;
    }
}
