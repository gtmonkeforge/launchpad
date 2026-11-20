using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private const string ReleasesHint = "When anyone uploads a release to a mod you own, or to one owned by a publishing group you're in. Useful for spotting uploads you didn't expect.";
    private const string AnnouncementsHint = "Occasional news from the staff team.";
    private const string AlwaysSentHint = "Account emails, like confirmation links, password resets and security alerts, are always sent.";

    private double DrawEmails(Ui ui, double left, double width)
    {
        if (Waiting(ui, left, width, out var bottom))
            return bottom;

        var email = _data.Str("email");
        var intro = email is { Length: > 0 }
            ? "Emails go to " + email + ". You can change it on monkeforge.org."
            : "Your account has no confirmed email address, so you won't get any of these. Add one on monkeforge.org.";
        var indented = width - 36;
        var firstTop = 44 + Card.HintHeight(intro, width) + 8;
        var secondTop = firstTop + 28 + Card.HintHeight(ReleasesHint, indented) + 12;
        var footerTop = secondTop + 28 + Card.HintHeight(AnnouncementsHint, indented) + 12;
        var height = footerTop + Card.HintHeight(AlwaysSentHint, width) + 20;

        Card card = Card.Open(ui, "emails-panel", left, TopMargin, width, height, "What we email you");
        card.Hint(0, 44, width, intro);
        card.Hint(18, firstTop + 28, indented, ReleasesHint);
        card.Hint(18, secondTop + 28, indented, AnnouncementsHint);
        card.Hint(0, footerTop, width, AlwaysSentHint, Palette.Faint);

        EmailToggle(ui, left, TopMargin + firstTop, "releases", "New releases on mods you manage");
        EmailToggle(ui, left, TopMargin + secondTop, "announcements", "MonkeForge announcements");
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
