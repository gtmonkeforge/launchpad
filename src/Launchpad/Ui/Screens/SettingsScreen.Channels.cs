using System.Text.RegularExpressions;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private const string ChannelHint = "Mod authors can share 25-character codes for private betas. Paste the whole code below.";
    private const string PurchasesHint = "Mods you buy show up in your Library, where you can install them. Receipts and disputes are on monkeforge.org.";

    private string _code = "";
    private string _unlockStatus = "";
    private string? _unlockUrl;
    private bool _unlocking;

    private double DrawChannels(Ui ui, double left, double width)
    {
        var height = 170 + Card.HintHeight(ChannelHint, width) + (_unlockStatus.Length > 0 ? 34 : 0);
        Card card = Card.Open(ui, "channels-panel", left, TopMargin, width, height, "Unlock a release channel");
        var top = 44 + card.Hint(0, 44, width, ChannelHint) + 10;

        ui.Pos(left + Card.Padding, TopMargin + top);
        ui.Size(Math.Min(width - Card.Padding * 2, 420), 38);
        ui.InputEx("##channel-code", ref _code, GoomeSharp.InputFlags.None, "XXXXX-XXXXX-XXXXX-XXXXX-XXXXX", 29);

        if (Widgets.Button(ui, "channels-unlock", left + Card.Padding, TopMargin + top + 52, _unlocking ? "Unlocking..." : "Unlock channel", ButtonKind.Primary, out _, !_unlocking && _code.Trim().Length > 0, 36))
            Unlock();

        if (_unlockStatus.Length > 0)
            card.Text(Card.Padding, top + 52 + 56, 14, Chrome.Clip(Fonts.Main, 14, _unlockStatus, width - 150), Palette.Muted);

        if (_unlockUrl != null && Widgets.Button(ui, "channels-open", left + width - Card.Padding - 110, TopMargin + top + 52 + 40, "Open it", ButtonKind.Subtle, out _))
            shell.OpenForge(_unlockUrl);

        return TopMargin + height;
    }

    private void Unlock()
    {
        var code = Regex.Replace(_code.ToUpperInvariant(), "[^A-Z0-9]", "");
        _unlocking = true;
        _unlockUrl = null;

        if (code.Length != 25)
        {
            _unlockStatus = "A channel code has 25 characters.";
            _unlocking = false;
            return;
        }

        var dashed = string.Join('-', Enumerable.Range(0, 5).Select(i => code.Substring(i * 5, 5)));

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await shell.App.Forge.SendJsonAsync(HttpMethod.Post, "/api/v1/channels/unlock", new { code = dashed });
                _unlockStatus = "Unlocked " + result.Str("name") + ".";
                _unlockUrl = result.Str("url");
                _code = "";
            }
            catch (UserFacingException ex)
            {
                _unlockStatus = ex.Message;
            }

            _unlocking = false;
            shell.Invalidate();
        });
    }

    private double DrawPurchases(Ui ui, double left, double width)
    {
        var height = 136 + Card.HintHeight(PurchasesHint, width);
        Card card = Card.Open(ui, "purchases-panel", left, TopMargin, width, height, "Your purchases");
        card.Hint(0, 44, width, PurchasesHint);
        var top = TopMargin + height - 58;

        if (Widgets.Button(ui, "purchases-library", left + Card.Padding, top, "Open Library", ButtonKind.Primary, out var buttonWidth, true, 36))
            shell.Router.Push(new Route(Page.Library));

        if (Widgets.Button(ui, "purchases-receipts", left + Card.Padding + buttonWidth + 10, top, "Receipts and disputes", ButtonKind.Subtle, out _, true, 36))
            shell.OpenForge((shell.User.Str("profile_url") ?? "/settings") + "#purchases");

        return TopMargin + height;
    }
}
