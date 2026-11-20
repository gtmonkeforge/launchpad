using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SupportScreen
{
    private string _to = "";
    private string _subject = "";
    private string _message = "";

    private double DrawCompose(Ui ui, double left, double width)
    {
        Item back = Chrome.Hit(ui, "support-compose-back", left, 28, 150, 24);
        Chrome.Text(ui.Canvas()!, Fonts.Main, 14, back.X, back.Y + 12, "< All tickets", back.Hovered ? Palette.Text : Palette.Muted);

        if (back.Clicked)
            _shell.Router.Push(new Route(Page.Support));

        Card.Open(ui, "support-compose", left, 70, width, 330, "New ticket");
        Field(ui, left, 118, width, "##support-to", "Email address", ref _to, 254);
        Field(ui, left, 168, width, "##support-subject", "Subject", ref _subject, 200);
        Field(ui, left, 218, width, "##support-message", "Message", ref _message, 4000);
        var ready = _to.Contains('@') && _subject.Trim().Length > 0 && _message.Trim().Length > 0;

        if (Widgets.Button(ui, "support-compose-send", left + Card.Padding, 276, "Send", ButtonKind.Primary, out _, ready, 36))
            Compose();

        return 400;
    }

    private void Field(Ui ui, double left, double y, double width, string id, string hint, ref string value, int maxBytes)
    {
        ui.Pos(left + Card.Padding, y);
        ui.Size(width - Card.Padding * 2, 38);
        ui.InputEx(id, ref value, InputFlags.None, hint, maxBytes);
    }

    private void Compose()
    {
        var body = new { to = _to.Trim(), subject = _subject.Trim(), body = _message.Trim() };

        Send(HttpMethod.Post, "/api/v1/admin/tickets", body, "Message sent.", result =>
        {
            _to = "";
            _subject = "";
            _message = "";
            _shell.Router.Push(new Route(Page.Support, result["id"]?.ToString()));
        });
    }
}
