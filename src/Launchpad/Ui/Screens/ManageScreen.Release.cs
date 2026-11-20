using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ManageScreen
{
    private double DrawReleaseTab(Ui ui, double width, double y)
    {
        y = _release.Draw(ui, width, y, true);
        y = FormKit.Error(ui, _error, y, width);

        if (FormKit.ActionButton(ui, "manage-publish-release", _busy ? "Uploading and scanning..." : "Publish release", FormKit.Left, y, true, out _, !_busy))
            PublishRelease();

        return y + 44;
    }

    private void PublishRelease()
    {
        _error = _release.Validate();

        if (_error != null)
            return;

        Run("Release published.", async () =>
        {
            using var form = new MultipartFormDataContent();
            _release.AddFields(form);
            using var response = await _shell.App.Forge.SendAsync(HttpMethod.Post, ModUrl("/releases"), form);
            _release.Version = "";
            _release.Notes = "";
            Array.Fill(_release.DllPaths, "");
            Array.Fill(_release.ZipPaths, "");
            await Refresh();
        });
    }
}
