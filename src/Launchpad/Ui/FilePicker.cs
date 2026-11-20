namespace Launchpad.Ui;

using Ui = GoomeSharp.Ui;

public sealed partial class FilePicker
{
    private static string _lastFolder = "";

    private readonly List<string> _chosen = [];
    private bool _open;
    private bool _focus;
    private string _title = "";
    private string[] _extensions = [];
    private bool _multiple;
    private bool _foldersOnly;
    private Action<IReadOnlyList<string>>? _done;
    private string _typed = "";
    private string _problem = "";

    public bool IsOpen => _open;

    public void Open(string title, string[] extensions, bool multiple, Action<IReadOnlyList<string>> done, string? startAt = null, bool foldersOnly = false)
    {
        _title = title;
        _extensions = extensions;
        _multiple = multiple;
        _foldersOnly = foldersOnly;
        _done = done;
        _chosen.Clear();
        _problem = "";
        _focus = true;
        _open = true;
        GoTo(FirstFolder(startAt));
    }

    public void Draw(Ui ui)
    {
        if (!ui.ModalBegin(_title, ref _open))
            return;

        ui.Label(_foldersOnly ? "Click a folder to open it, then use this folder." : "Click a folder to open it and a file to pick it. You can also type a path.");
        ui.Space(4);

        if (ui.Button("Up") && Directory.GetParent(_folder) is { } parent)
            GoTo(parent.FullName);

        ui.SameLine();

        if (ui.Button("Home"))
            GoTo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        ui.SameLine();
        ui.Width(340);

        if (_focus)
            ui.SetFocus();

        if (ui.InputEx("##picker-path", ref _typed, GoomeSharp.InputFlags.EnterReturns, "Type or paste a path", 1024) != 0)
        {
            _focus = false;
            GoToTyped();
        }

        ui.Space(6);
        DrawList(ui);
        ui.Space(6);

        if (_problem.Length > 0)
            ui.Label(_problem);

        if (!_foldersOnly)
            ui.Label(_chosen.Count == 0 ? "Pick a file." : _chosen.Count + (_chosen.Count == 1 ? " file picked." : " files picked."));

        if (ui.Button(_foldersOnly ? "Use this folder" : "Select"))
            Finish(ui);

        ui.SameLine();

        if (ui.Button("Cancel"))
            ui.PopupClose();

        ui.ModalEnd();
    }

    private void DrawList(Ui ui)
    {
        ui.ScrollBegin("##picker-list", 280);

        if (_entries.Count == 0)
            ui.Label(_foldersOnly ? "No folders in here." : "Nothing here that can be picked.");

        foreach (var entry in _entries)
        {
            var chosen = _chosen.Contains(entry.Path);
            var text = entry.IsFolder ? entry.Name + "/" : entry.Name + "    " + Screens.UploadFiles.Size(entry.Size);

            if (!ui.Selectable(text + "##" + entry.Path, chosen))
                continue;

            if (entry.IsFolder)
                GoTo(entry.Path);
            else
                Toggle(entry.Path);

            break;
        }

        ui.ScrollEnd();
    }

    private void Finish(Ui ui)
    {
        var result = _foldersOnly ? [_folder] : _chosen.ToList();

        if (result.Count == 0)
        {
            _problem = "Pick at least one file first.";
            return;
        }

        _lastFolder = _folder;
        ui.PopupClose();
        _done?.Invoke(result);
    }

    private void Toggle(string path)
    {
        if (_chosen.Remove(path))
            return;

        if (!_multiple)
            _chosen.Clear();

        _chosen.Add(path);
        _problem = "";
    }
}
