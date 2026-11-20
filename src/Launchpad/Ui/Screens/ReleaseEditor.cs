using System.Text.RegularExpressions;
using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ReleaseEditor(Shell shell, FilePicker picker)
{
    public static readonly string[] LoaderIds = ["bepinex5", "bepinex6", "melonloader"];
    private static readonly string[] LoaderNames = ["BepInEx 5", "BepInEx 6", "MelonLoader"];

    [GeneratedRegex(@"^[0-9][a-zA-Z0-9.+_-]{0,63}$")]
    private static partial Regex VersionPattern();

    public string Version = "";
    public string Notes = "";
    public string? CurrentVersion;
    public string[] Channels = ["stable"];
    public int Channel;
    public readonly string[] DllPaths = ["", "", ""];
    public readonly string[] ZipPaths = ["", "", ""];

    public string ChannelName => Channels[Math.Clamp(Channel, 0, Channels.Length - 1)];

    public double Draw(Ui ui, double width, double y, bool withChannel)
    {
        y = FormKit.Hint(ui, "Releases are immutable: versions, files and changelogs can't be edited after publishing. Ship a new version to update downloads.", y, width);
        var hint = CurrentVersion != null ? "Current: v" + CurrentVersion : "The version players see, such as 1.0.0.";

        if (withChannel)
        {
            var half = width / 2 - 6;
            Item version = Chrome.Hit(ui, "release-version-label", FormKit.Left, y + 6, half, 18);
            Chrome.BoldText(ui.Canvas()!, Fonts.Main, 13, version.X, version.Y + 9, "Version", Palette.Faint);
            Item channel = Chrome.Hit(ui, "release-channel-label", FormKit.Left + half + 12, y + 6, half, 18);
            Chrome.BoldText(ui.Canvas()!, Fonts.Main, 13, channel.X, channel.Y + 9, "Release channel", Palette.Faint);
            ui.Pos(FormKit.Left, y + 28);
            ui.Size(half, FormKit.RowHeight);
            ui.InputEx("##release-version", ref Version, InputFlags.None, "1.0.0", 128);
            ui.Pos(FormKit.Left + half + 12, y + 28);
            ui.Size(half, FormKit.RowHeight);
            ui.Combo("##release-channel", ref Channel, Channels);
            y = FormKit.Hint(ui, hint, y + 75, width);
        }
        else
            y = FormKit.Field(ui, "version", "Version", ref Version, y + 6, width, "1.0.0", hint, 128);

        y = DrawDownloads(ui, width, y);
        y = DrawGitHub(ui, width, y);
        return FormKit.TextArea(ui, "notes", "Changelog (optional)", ref Notes, y + 8, width, 110, null, 10000);
    }

    private double DrawDownloads(Ui ui, double width, double y)
    {
        y = FormKit.Label(ui, "Downloads (one DLL, one ZIP, or both per loader, 50 MB each)", y);
        var half = (width - 120) / 2;

        for (var i = 0; i < 3; i++)
        {
            Item name = Chrome.Hit(ui, "release-loader-" + i, FormKit.Left, y, 110, FormKit.RowHeight);
            Chrome.BoldText(ui.Canvas()!, Fonts.Main, 14, name.X, name.Y + 19.5, LoaderNames[i], Palette.Text);
            var slot = i;
            DrawPath(ui, "dll-" + i, ref DllPaths[i], ".dll", FormKit.Left + 120, y, half, p => DllPaths[slot] = p);
            DrawPath(ui, "zip-" + i, ref ZipPaths[i], ".zip", FormKit.Left + 120 + half + 12, y, half - 12, p => ZipPaths[slot] = p);
            y += 47;
        }

        return y;
    }

    private void DrawPath(Ui ui, string id, ref string path, string extension, double x, double y, double width, Action<string> set)
    {
        ui.Pos(x, y);
        ui.Size(width - 64, FormKit.RowHeight);
        ui.InputEx("##release-" + id, ref path, InputFlags.None, "Path to a " + extension, 1000);

        if (Widgets.Button(ui, "release-pick-" + id, x + width - 58, y + 2, "Pick", ButtonKind.Subtle, out _, true, 35))
            picker.Open("Choose a " + extension + " file", [extension], false, picked => set(picked[0]));
    }

    public string? Validate()
    {
        if (!VersionPattern().IsMatch(Version.Trim()))
            return "Use a version such as 1.0.0.";

        if (Enumerable.Range(0, 3).All(i => DllPaths[i].Trim().Length == 0 && ZipPaths[i].Trim().Length == 0))
            return "Attach downloads for at least one mod loader.";

        for (var i = 0; i < 3; i++)
        {
            var problem = UploadFiles.Check(DllPaths[i], 50 * UploadFiles.Megabyte, [".dll"], "Each download must be 50 MB or smaller.", "Each loader supports one DLL, one ZIP, or one of each.")
                ?? UploadFiles.Check(ZipPaths[i], 50 * UploadFiles.Megabyte, [".zip"], "Each download must be 50 MB or smaller.", "Each loader supports one DLL, one ZIP, or one of each.");

            if (problem != null)
                return problem;
        }

        return null;
    }

    public void AddFields(MultipartFormDataContent form)
    {
        form.Add(new StringContent(Version.Trim()), "version");
        form.Add(new StringContent(ChannelName), "channel");
        form.Add(new StringContent(Notes), "notes");

        for (var i = 0; i < 3; i++)
        {
            UploadFiles.Add(form, "downloads_" + LoaderIds[i], DllPaths[i]);
            UploadFiles.Add(form, "downloads_" + LoaderIds[i], ZipPaths[i]);
        }
    }
}
