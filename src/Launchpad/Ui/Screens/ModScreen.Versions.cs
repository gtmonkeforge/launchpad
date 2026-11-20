using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private const double FileRow = 57;

    private double DrawVersions(Ui ui, double left, double width, double y)
    {
        var releases = _mod!["releases"] as JsonArray ?? [];

        if (releases.Count == 0)
            return DrawEmptyVersions(ui, left, width, y);

        for (var i = 0; i < releases.Count; i++)
            y = DrawRelease(ui, left, width, y, releases[i]!, i) + 12;

        return y;
    }

    private double DrawEmptyVersions(Ui ui, double left, double width, double y)
    {
        Item box = Area(ui, "mod-no-releases", left, y, width, 100);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, 100, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 17, box.X + 22, box.Y + 36, "No releases yet", Palette.Text);
        Chrome.Text(c, Fonts.Main, 14, box.X + 22, box.Y + 66, "Releases published to this channel will appear here.", Palette.Muted);
        return y + 100;
    }

    private double DrawRelease(Ui ui, double left, double width, double y, JsonNode release, int index)
    {
        var files = release["downloads"] as JsonArray ?? [];
        var notes = WrapLines(Fonts.Main, 14, release.Str("notes") ?? "", width - 44, 30);
        var height = 18 + 34 + files.Count * FileRow + (notes.Count > 0 ? 8 + notes.Count * 22 : 0) + 8;
        Item card = Area(ui, "mod-release-" + index, left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, card.X, card.Y, width, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);

        var x = card.X + 22;
        var center = card.Y + 18 + 17;
        x += Chrome.BoldText(c, Fonts.Mono, 15, x, center, "v" + release.Str("version"), Palette.Text) + 10;

        if (release.Str("version") == _mod.Str("current_version"))
            x += Pill(c, x, center, "Latest", Palette.Good) + 6;

        foreach (var loader in release["loaders"] as JsonArray ?? [])
            x += Pill(c, x, center, LoaderLabel(loader?.GetValue<string>()), Palette.Muted) + 6;

        var date = DateText(release.Str("released_at"));
        Chrome.Text(c, Fonts.Mono, 12.5, card.X + width - 22 - Fonts.Mono.Measure(12.5, date), center, date, Palette.Faint);

        var fileTop = card.Y + 18 + 34;

        for (var i = 0; i < files.Count; i++)
            DrawFile(c, files[i]!, card.X + 22, fileTop + i * FileRow, width - 44);

        for (var i = 0; i < notes.Count; i++)
            Chrome.Text(c, Fonts.Main, 14, card.X + 22, fileTop + files.Count * FileRow + 19 + i * 22, notes[i], Palette.Muted);

        return y + height;
    }

    private static void DrawFile(Canvas c, JsonNode file, double x, double y, double width)
    {
        Chrome.Box(c, x, y, width, FileRow - 7, 8, Palette.Surface2, Palette.Line);
        var kind = (file.Str("kind") ?? "").ToUpperInvariant();
        c.DrawRRect(x + 10, y + 8, 34, 34, 7, Palette.Bg);
        Chrome.BoldText(c, Fonts.Mono, 11.5, x + 27 - Fonts.Mono.Measure(11.5, kind) / 2, y + 25, kind, Palette.Muted);
        var name = Chrome.Clip(Fonts.Main, 14, file.Str("filename") ?? "", width - 110);
        Chrome.Text(c, Fonts.Main, 14, x + 56, y + 18, name, Palette.Text);
        Chrome.Text(c, Fonts.Mono, 12, x + 56, y + 36, LoaderLabel(file.Str("loader")) + " · " + Size(file.Long("size")), Palette.Faint);
        Icons.Download(c, x + width - 22, y + 25, Palette.Muted);
    }
}
