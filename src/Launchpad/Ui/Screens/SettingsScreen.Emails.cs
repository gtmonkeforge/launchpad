using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private double DrawEmails(Ui ui, double left, double width)
    {
        if (Waiting(ui, left, width, out var bottom))
            return bottom;

        var email = _data.Str("email");
        var intro = email is { Length: > 0 }
            ? "Emails go to " + email + ". You can change it on monkeforge.org."
            : "Your account has no confirmed email address, so you won't get any of these. Add one on monkeforge.org.";
        var releases = "When anyone uploads a release to a mod you own, or to one owned by a publishing group you're in. Useful for spotting uploads you didn't expect.";
        var news = "Occasional news from the staff team.";
        var always = "Account emails, like confirmation links, password resets and security alerts, are always sent.";
        var narrow = width - 36;
        var first = 44 + Card.HintHeight(intro, width) + 8;
        var second = first + 28 + Card.HintHeight(releases, narrow) + 12;
        var last = second + 28 + Card.HintHeight(news, narrow) + 12;
        var height = last + Card.HintHeight(always, width) + 20;

        Card card = Card.Open(ui, "emails-panel", left, TopMargin, width, height, "What we email you");
        card.Hint(0, 44, width, intro);
        card.Hint(18, first + 28, narrow, releases);
        card.Hint(18, second + 28, narrow, news);
        card.Hint(0, last, width, always, Palette.Faint);

        EmailToggle(ui, left, TopMargin + first, "releases", "New releases on mods you manage");
        EmailToggle(ui, left, TopMargin + second, "announcements", "MonkeForge announcements");
        return TopMargin + height;
    }

    private void EmailToggle(Ui ui, double left, double y, string key, string title)
    {
        var on = _data?["notifications"].Bool(key) ?? false;
        ui.Pos(left + Card.Padding, y);

        if (!ui.Checkbox(title + "##" + key, ref on))
            return;

        _data!["notifications"]![key] = on;
        Send(HttpMethod.Put, "/api/v1/account/notifications", new Dictionary<string, bool> { [key] = on }, "Email settings saved.", false);
    }
}
