using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ManageScreen
{
    private int _versionChannel;
    private bool _versionsLoading;

    private async Task LoadVersions()
    {
        _versionsLoading = true;
        _shell.Invalidate();

        try
        {
            var name = _versionChannel < _channels.Count ? _channels[_versionChannel].Str("name") ?? "stable" : "stable";
            var result = await _shell.App.Forge.GetJsonAsync(ModUrl("/releases?channel=" + Uri.EscapeDataString(name)));
            _versions = result["items"] as JsonArray ?? [];
        }
        catch (UserFacingException ex)
        {
            _error = ex.Message;
        }

        _versionsLoading = false;
        _shell.Invalidate();
    }

    private double DrawVersions(Ui ui, double width, double y)
    {
        var names = _channels.Select(channel => channel.Str("name") ?? "stable").ToArray();
        y = FormKit.Label(ui, "Channel", y);
        ui.Pos(FormKit.Left, y);
        ui.Size(220, FormKit.RowHeight);

        if (ui.Combo("##manage-version-channel", ref _versionChannel, names))
            _ = Task.Run(LoadVersions);

        y += FormKit.RowHeight + 14;
        y = FormKit.Hint(ui, "Published versions can't be changed or removed by their publisher. If a release is broken, ship a fixed version or ask the MonkeForge staff to pull it.", y, width);

        if (_versionsLoading)
        {
            ui.Pos(FormKit.Left, y + 10);
            ui.Spinner(24);
            return y + 50;
        }

        if (_versions.Count == 0)
            return FormKit.Hint(ui, "No releases in this channel yet.", y, width);

        for (var i = 0; i < _versions.Count; i++)
            y = DrawVersion(ui, _versions[i]!, i, y, width) + 10;

        return y;
    }

    private double DrawVersion(Ui ui, JsonNode release, int index, double y, double width)
    {
        var downloads = release["downloads"] as JsonArray ?? [];
        var notes = Chrome.Wrap(Fonts.Main, 13.5, release.Str("notes") ?? "", width - 40, 3);
        var height = 58 + downloads.Count * 22 + notes.Count * 20;
        Item box = Chrome.Hit(ui, "manage-version-" + index, FormKit.Left, y, width, height);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, width, height, Palette.Radius, Palette.Surface, Palette.Line);
        Chrome.BoldText(c, Fonts.Main, 16, box.X + 18, box.Y + 24, "v" + release.Str("version"), Palette.Text);
        var released = release.Str("released_at");

        if (DateTimeOffset.TryParse(released, out var when))
            Chrome.Text(c, Fonts.Main, 13, box.X + width - 18 - Fonts.Main.Measure(13, Format.Ago(when)), box.Y + 24, Format.Ago(when), Palette.Faint);

        var top = 50.0;

        foreach (var download in downloads)
        {
            var line = download.Str("filename") + "  " + UploadFiles.Size(download.Long("size")) + "  " + download.Str("loader");
            Chrome.Text(c, Fonts.Mono, 12.5, box.X + 18, box.Y + top, Chrome.Clip(Fonts.Mono, 12.5, line, width - 40), Palette.Muted);
            top += 22;
        }

        foreach (var line in notes)
        {
            Chrome.Text(c, Fonts.Main, 13.5, box.X + 18, box.Y + top + 4, line, Palette.Faint);
            top += 20;
        }

        return y + height;
    }
}
