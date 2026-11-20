using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private bool _lightbox;

    private void DrawLightbox(Ui ui)
    {
        var shots = Strings(_mod?["screenshots"]);

        if (shots.Length == 0 || !ui.ModalBegin("Screenshot " + (_shot + 1) + " of " + shots.Length, ref _lightbox))
            return;

        var width = Math.Min(900, _stageWidth);
        var height = width * 9 / 16;
        ui.Width(width);
        ui.Custom("lightbox-image", width, height, ItemFlags.None, out Item image);
        Canvas c = ui.Canvas()!;
        c.DrawRect(image.X, image.Y, width, height, Palette.Bg);
        DrawFitted(c, _shell.Images.Get(shots[_shot]), image.X, image.Y, width, height);

        if (shots.Length > 1 && image.Clicked)
            _shot = (_shot + 1) % shots.Length;

        ui.Space(6);

        if (shots.Length > 1)
        {
            if (ui.Button("Previous"))
                _shot = (_shot - 1 + shots.Length) % shots.Length;

            ui.SameLine();

            if (ui.Button("Next"))
                _shot = (_shot + 1) % shots.Length;

            ui.SameLine();
        }

        if (ui.Button("Close"))
            ui.PopupClose();

        ui.ModalEnd();
    }
}
