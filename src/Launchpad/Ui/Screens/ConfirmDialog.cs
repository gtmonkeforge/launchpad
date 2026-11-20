namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed class ConfirmDialog
{
    private bool _open;
    private string _title = "";
    private string _text = "";
    private string _label = "";
    private Action? _run;

    public void Ask(string title, string text, string label, Action run)
    {
        _title = title;
        _text = text;
        _label = label;
        _run = run;
        _open = true;
    }

    public void Draw(Ui ui)
    {
        if (!ui.ModalBegin(_title, ref _open))
            return;

        ui.Width(460);
        ui.Wrapped(_text);
        ui.Space(10);

        if (ui.Button(_label))
        {
            var run = _run;
            ui.PopupClose();
            run?.Invoke();
        }

        ui.SameLine();

        if (ui.Button("Cancel"))
            ui.PopupClose();

        ui.ModalEnd();
    }
}
