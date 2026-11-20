using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen : IScreen
{
    private const double SideMargin = 30;
    private const double RightGutter = 57;
    private const double Gap = 18;

    private readonly Shell _shell;
    private readonly string _guid;
    private string _channel;
    private double _ox;
    private double _oy;

    public ModScreen(Shell shell, string? arg)
    {
        _shell = shell;
        var parts = (arg ?? "").Split('?', 2);
        _guid = parts[0];
        _channel = "stable";

        foreach (var pair in parts.Length > 1 ? parts[1].Split('&') : [])
        {
            if (pair.StartsWith("channel=", StringComparison.Ordinal))
                _channel = Uri.UnescapeDataString(pair["channel=".Length..]);
        }

        shell.App.Mods.Changed += LoadInstalled;
        Load();
    }

    public void Reload() => Load();

    public void Dispose()
    {
        _shell.App.Mods.Changed -= LoadInstalled;
    }

    public void Draw(Ui ui, double width, double height)
    {
        ui.ScrollBegin("mod", height);
        ui.Pos(0, 0);
        ui.Custom("mod-origin", 1, 1, ItemFlags.NoHit, out Item origin);
        _ox = origin.X;
        _oy = origin.Y;
        DrawBackdrop(ui, width);

        var contentWidth = Math.Max(640, width - SideMargin - RightGutter);
        var bottom = DrawBackLink(ui, 22);

        if (_mod == null)
            bottom = DrawLoading(ui, contentWidth, bottom + 20);
        else
            bottom = DrawMod(ui, contentWidth, bottom + 14);

        _shell.Footer.Finish(ui, "mod-end", bottom, height, width);
        ui.ScrollEnd();
    }

    private double DrawBackLink(Ui ui, double y)
    {
        Item item = Chrome.Hit(ui, "mod-back", SideMargin - 6, y, 110, 30);
        Canvas c = ui.Canvas()!;
        var color = item.Hovered ? Palette.Text : Palette.Muted;
        Icons.Back(c, item.X + 14, item.Y + 15, color);
        Chrome.Text(c, Fonts.Main, 14, item.X + 28, item.Y + 15, "All mods", color);

        if (item.Clicked)
        {
            if (_shell.Router.CanGoBack)
                _shell.Router.Back();
            else
                _shell.Router.Push(new Route(Page.Browse));
        }

        return y + 30;
    }

    private double DrawLoading(Ui ui, double width, double y)
    {
        if (_loading)
        {
            ui.Pos(SideMargin + width / 2 - 12, y + 80);
            ui.Spinner(24);
            return y + 140;
        }

        Item box = Area(ui, "mod-error", SideMargin, y, 420, 130);
        Canvas c = ui.Canvas()!;
        Chrome.BoldText(c, Fonts.Main, 17, box.X, box.Y + 24, "Couldn't load this mod", Palette.Text);
        Chrome.Text(c, Fonts.Main, 14.5, box.X, box.Y + 54, Chrome.Clip(Fonts.Main, 14.5, _error ?? "Something went wrong.", 600), Palette.Muted);

        if (Button(ui, "mod-error-open", "Open on monkeforge.org", SideMargin, y + 80, 210, 38, ButtonStyle.Plain))
            _shell.OpenForge("/view/" + Uri.EscapeDataString(_guid));

        return y + 130;
    }

    private double DrawMod(Ui ui, double width, double y)
    {
        var top = y;

        if (Status != "approved")
            y = DrawNotice(ui, width, y) + 12;

        y = DrawBanner(ui, width, y);
        y = DrawHero(ui, width, y) + 16;
        y = DrawStats(ui, width, y) + 26;

        var sideWidth = width >= 940 ? 330.0 : 290.0;
        var mainWidth = width - sideWidth - Gap;
        var mainBottom = DrawMain(ui, SideMargin, mainWidth, y);
        var sideBottom = DrawSidebar(ui, SideMargin + mainWidth + Gap, sideWidth, y);
        return Math.Max(mainBottom, sideBottom);
    }

    private double DrawBanner(Ui ui, double width, double y)
    {
        Bitmap? banner = _shell.Images.Get(_mod.Str("banner"));

        if (banner == null)
            return y;

        if (!ReferenceEquals(banner, _bannerSource) || Math.Abs(width - _bannerWidth) > 1)
        {
            _bannerSource = banner;
            _bannerWidth = width;
            _bannerCrop = Backdrop.Cover(banner, width / 220);
        }

        Item area = Area(ui, "mod-banner", SideMargin, y, width, 220);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, area.X, area.Y, width, 220, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        c.DrawBitmap(_bannerCrop!, area.X + 1, area.Y + 1, width - 2, 218, Palette.RadiusLarge - 1);
        return y + 232;
    }

    private Bitmap? _bannerCrop;
    private Bitmap? _bannerSource;
    private double _bannerWidth;

    private double DrawNotice(Ui ui, double width, double y)
    {
        var text = Status == "pending" ? "This mod is awaiting review before it becomes publicly available." : "This mod is not publicly available.";
        Item box = Area(ui, "mod-notice", SideMargin, y, width, 42);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, 42, Palette.Radius, 0x14E5B85C, 0x5CE5B85C);
        Chrome.Text(c, Fonts.Main, 14, box.X + 16, box.Y + 21, text, Palette.Warn);
        return y + 42;
    }

    private string? Status => _mod.Str("status");

    private double DrawStats(Ui ui, double width, double y)
    {
        var loaders = _mod!["loaders"] is JsonArray list && list.Count > 0 ? list.Count : 1;
        var updated = Current == null ? "-" : Format.Ago(ParseDate(Current.Str("released_at")));
        var cells = new (string Label, string Value)[]
        {
            ("Downloads", Format.Number(_mod.Long("downloads"))),
            ("Rating", _rating),
            ("Version", _mod.Str("current_version") is { Length: > 0 } version ? version : "-"),
            ("Updated", updated),
            ("Loaders", loaders.ToString()),
        };

        Item strip = Area(ui, "mod-stats", SideMargin, y, width, 46);
        Canvas c = ui.Canvas()!;
        c.DrawRect(strip.X, strip.Y, width, 1, Palette.Line);
        c.DrawRect(strip.X, strip.Y + 45, width, 1, Palette.Line);
        var x = strip.X;

        foreach (var (label, value) in cells)
        {
            x += Chrome.Text(c, Fonts.Main, 13, x, strip.Y + 23, label, Palette.Muted) + 8;
            x += Chrome.Text(c, Fonts.Mono, 14, x, strip.Y + 23, value, Palette.Text) + 28;
        }

        return y + 46;
    }

    private void DrawBackdrop(Ui ui, double width)
    {
        Bitmap? picture = _shell.Images.Get(_mod.Str("banner")) ?? _shell.Images.Get(_mod.Str("logo"));

        if (picture == null)
            return;

        if (!ReferenceEquals(picture, _backdropSource))
        {
            _backdropSource = picture;
            _backdrop = Backdrop.Blurred(picture);
        }

        Canvas c = ui.Canvas()!;
        c.DrawBitmap(_backdrop!, _ox, _oy, width, 460, 0, 0x38FFFFFF);
        c.DrawRRectGrad(_ox, _oy + 220, width, 240, 0, 0x00111214, Palette.Bg);
    }

    private Bitmap? _backdrop;
    private Bitmap? _backdropSource;

    private static DateTimeOffset ParseDate(string? text) =>
        DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var when) ? when : DateTimeOffset.UtcNow;
}
