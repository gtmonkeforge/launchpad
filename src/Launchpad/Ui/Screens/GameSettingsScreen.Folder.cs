using Launchpad.Game;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class GameSettingsScreen
{
    private bool _folderOpen;
    private string _typedPath = "";
    private bool _focusPath;

    private static string Cleaned(string path)
    {
        path = path.Trim().Trim('"', '\'');

        if (path.StartsWith("~/", StringComparison.Ordinal))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);

        return path;
    }

    private void DrawFolderDialog(Ui ui)
    {
        if (!ui.ModalBegin("Choose your game folder", ref _folderOpen))
            return;

        var path = Cleaned(_typedPath);
        var valid = GameLocator.IsGameFolder(path);

        ui.Wrapped($"Type or paste the folder that contains {GameLocator.Executable}.");
        ui.Space(4);
        ui.Width(520);

        if (_focusPath)
            ui.SetFocus();

        if (ui.Input("##game-path", ref _typedPath, 1024))
            _focusPath = false;

        if (path.Length > 0)
            ui.Label(valid ? "Found it. This folder has the game." : $"This folder doesn't contain {GameLocator.Executable}.");

        ui.Space(6);

        if (ui.Button("Use this folder") && valid)
        {
            SelectGame(Path.GetFullPath(path));
            ui.PopupClose();
        }

        ui.SameLine();

        if (ui.Button("Cancel"))
            ui.PopupClose();

        ui.ModalEnd();
    }
}
