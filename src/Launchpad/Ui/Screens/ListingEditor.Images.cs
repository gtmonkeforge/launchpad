using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ListingEditor
{
    private static readonly string[] ImageTypes = [".png", ".jpg", ".jpeg", ".webp"];
    private const int MaxShots = 20;

    private string _shotPath = "";

    public string Logo = "";
    public string Banner = "";
    public readonly List<string> Screenshots = [];
    public string? ExistingLogo;
    public string? ExistingBanner;
    public List<string> ExistingShots = [];
    public bool RemoveLogo;
    public bool RemoveBanner;
    public bool RemoveShots;

    private double DrawImages(Ui ui, double width, double y)
    {
        y = DrawImage(ui, "logo", "Logo (optional, square, 5 MB)", ref Logo, ExistingLogo, ref RemoveLogo, 56, y, width);
        y = DrawImage(ui, "banner", "Banner (optional, wide, 5 MB)", ref Banner, ExistingBanner, ref RemoveBanner, 112, y, width);
        return DrawShots(ui, width, y);
    }

    private double DrawImage(Ui ui, string id, string label, ref string path, string? existing, ref bool remove, double thumbWidth, double y, double width)
    {
        y = FormKit.Label(ui, label, y);
        ui.Pos(FormKit.Left, y);
        ui.Size(width - 100, FormKit.RowHeight);
        ui.InputEx("##listing-" + id, ref path, InputFlags.None, "Path to a PNG, JPG or WebP file", 1000);

        if (Widgets.Button(ui, "listing-browse-" + id, FormKit.Left + width - 90, y + 2, "Browse", ButtonKind.Subtle, out _, true, 35))
            picker.Open("Choose an image", ImageTypes, false, picked => SetImage(id, picked[0]));

        y += FormKit.RowHeight + 8;

        if (existing != null)
        {
            Item thumb = Chrome.Hit(ui, "listing-thumb-" + id, FormKit.Left, y, thumbWidth, 56);
            Canvas c = ui.Canvas()!;
            Bitmap? picture = remove ? null : shell.Images.Get(existing);

            if (picture != null)
                c.DrawBitmap(picture, thumb.X, thumb.Y, thumbWidth, 56, 8);
            else
                Chrome.Box(c, thumb.X, thumb.Y, thumbWidth, 56, 8, Palette.Surface2, Palette.Line);

            ui.Pos(FormKit.Left + thumbWidth + 16, y + 16);
            ui.Checkbox("Remove the current " + id, ref remove);
            y += 64;
        }

        return y + 8;
    }

    private void SetImage(string id, string path)
    {
        if (id == "logo")
            Logo = path;
        else
            Banner = path;

        shell.Invalidate();
    }

    private double DrawShots(Ui ui, double width, double y)
    {
        y = FormKit.Label(ui, "Screenshots (up to 20, new files replace the current set)", y);

        if (ExistingShots.Count > 0)
        {
            for (var i = 0; i < ExistingShots.Count; i++)
            {
                var x = FormKit.Left + i % 5 * 128;
                Item thumb = Chrome.Hit(ui, "listing-existing-" + i, x, y + i / 5 * 72, 120, 64);
                Bitmap? picture = RemoveShots ? null : shell.Images.Get(ExistingShots[i]);

                if (picture != null)
                    ui.Canvas()!.DrawBitmap(picture, thumb.X, thumb.Y, 120, 64, 8);
                else
                    Chrome.Box(ui.Canvas()!, thumb.X, thumb.Y, 120, 64, 8, Palette.Surface2, Palette.Line);
            }

            y += (ExistingShots.Count + 4) / 5 * 72 + 4;
            ui.Pos(FormKit.Left, y);
            ui.Checkbox("Remove the current screenshots", ref RemoveShots);
            y += 36;
        }

        for (var i = 0; i < Screenshots.Count; i++)
            y = DrawShotRow(ui, i, y, width);

        ui.Pos(FormKit.Left, y);
        ui.Size(width - 180, FormKit.RowHeight);
        ui.InputEx("##listing-shot-path", ref _shotPath, InputFlags.None, "Type or paste an image path", 1000);

        if (Widgets.Button(ui, "listing-shot-add", FormKit.Left + width - 170, y + 2, "Add", ButtonKind.Subtle, out var addWidth, true, 35))
            AddTyped();

        if (Widgets.Button(ui, "listing-shot-browse", FormKit.Left + width - 170 + addWidth + 8, y + 2, "Browse", ButtonKind.Subtle, out _, true, 35))
            picker.Open("Choose screenshots", ImageTypes, true, AddPicked);

        return y + FormKit.RowHeight + 16;
    }

    private double DrawShotRow(Ui ui, int i, double y, double width)
    {
        Item row = Chrome.Hit(ui, "listing-shot-row-" + i, FormKit.Left, y, width, 34);
        Chrome.Box(ui.Canvas()!, row.X, row.Y, width, 34, Palette.Radius, Palette.Surface, Palette.Line);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 13.5, row.X + 12, row.Y + 17, (i + 1) + ".  " + Chrome.Clip(Fonts.Main, 13.5, Path.GetFileName(Screenshots[i]), width - 250), Palette.Text);
        var buttons = FormKit.Left + width - 230;

        if (i > 0 && Widgets.Button(ui, "listing-shot-up-" + i, buttons, y + 3, "Up", ButtonKind.Subtle, out var upWidth, true, 28))
            (Screenshots[i - 1], Screenshots[i]) = (Screenshots[i], Screenshots[i - 1]);

        if (i < Screenshots.Count - 1 && Widgets.Button(ui, "listing-shot-down-" + i, buttons + 58, y + 3, "Down", ButtonKind.Subtle, out _, true, 28))
            (Screenshots[i + 1], Screenshots[i]) = (Screenshots[i], Screenshots[i + 1]);

        if (Widgets.Button(ui, "listing-shot-remove-" + i, buttons + 128, y + 3, "Remove", ButtonKind.Danger, out _, true, 28))
            Screenshots.RemoveAt(i);

        return y + 40;
    }

    private void AddTyped()
    {
        var path = _shotPath.Trim().Trim('"', '\'');

        if (path.Length == 0 || Screenshots.Count >= MaxShots)
            return;

        Screenshots.Add(path);
        _shotPath = "";
    }

    private void AddPicked(IReadOnlyList<string> paths)
    {
        foreach (var path in paths.Where(path => !Screenshots.Contains(path)).Take(MaxShots - Screenshots.Count))
            Screenshots.Add(path);

        shell.Invalidate();
    }
}
