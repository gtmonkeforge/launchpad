using Launchpad.Infrastructure;
namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private const string IdHint = "Share this with mod authors who want to add you to a private release channel.";
    private const string SessionHint = "You stay signed in for 30 days on this computer. Signing out ends this session only. To sign out everywhere, use the security settings on monkeforge.org.";

    private double DrawAccount(Ui ui, double left, double width)
    {
        var user = shell.User!;
        var name = user.Str("display_name") ?? user.Str("name") ?? "?";
        var details = "@" + user.Str("username") + (user.Str("discord_username") is { } discord ? " · Discord @" + discord : " · Email account");
        var y = TopMargin;

        Card profile = Card.Open(ui, "account-profile", left, y, width, 96, "");
        profile.Avatar(shell.Images.Get(user.Str("avatar")), 22, 24, 48, name);
        profile.Bold(84, 36, 16, Chrome.Clip(Fonts.Main, 16, name, width - 84 - 150), Palette.Text);
        profile.Text(84, 60, 13, Chrome.Clip(Fonts.Mono, 13, details, width - 84 - 150), Palette.Muted, true);

        if (user.Str("profile_url") is { Length: > 0 } url && Card.Button(ui, "View profile", left + width - 142, y + 30, 120))
            shell.OpenForge(url);

        y += 96 + 16;
        var idHeight = 130 + Card.HintHeight(IdHint, width) - 36;
        Card id = Card.Open(ui, "account-id", left, y, width, idHeight, "Account ID");
        var below = 44 + id.Hint(0, 44, width, IdHint);
        id.Text(Card.Padding, below + 22, 13.5, user.Str("id") ?? "", Palette.Text, true);

        if (Card.Button(ui, "Copy", left + width - 102, y + below + 5, 80))
        {
            shell.Window.Goome.Clipboard = user.Str("id") ?? "";
            shell.Toast("Account ID copied.");
        }

        y += idHeight + 16;
        var sessionHeight = 120 + Card.HintHeight(SessionHint, width);
        Card session = Card.Open(ui, "account-session", left, y, width, sessionHeight, "Session");
        session.Hint(0, 44, width, SessionHint);

        if (Card.Button(ui, "Sign out", left + Card.Padding, y + sessionHeight - 56, 110))
        {
            shell.SignOut();
            shell.Router.Push(new Route(Page.Browse));
        }

        return y + sessionHeight;
    }
}
