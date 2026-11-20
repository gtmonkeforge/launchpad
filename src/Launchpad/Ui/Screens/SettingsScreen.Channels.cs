using System.Text.RegularExpressions;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private const string ChannelHint = "Mod authors can share 25-character codes for private betas. Paste the whole code into the first box.";

    private readonly string[] _codeParts = ["", "", "", "", ""];
    private string _unlockStatus = "";
    private string? _unlockUrl;
    private bool _unlocking;

    private double DrawChannels(Ui ui, double left, double width)
    {
        var height = 170 + Card.HintHeight(ChannelHint, width) + (_unlockStatus.Length > 0 ? 34 : 0);
        Card card = Card.Open(ui, "channels-panel", left, TopMargin, width, height, "Unlock a release channel");
        var top = 44 + card.Hint(0, 44, width, ChannelHint) + 10;
        DrawCodeBoxes(ui, left + Card.Padding, TopMargin + top, Math.Min(width - Card.Padding * 2, 520));
        var code = string.Concat(_codeParts);

        if (Widgets.Button(ui, "channels-unlock", left + Card.Padding, TopMargin + top + 52, _unlocking ? "Unlocking..." : "Unlock channel", ButtonKind.Primary, out _, !_unlocking && code.Length > 0, 36, Glyphs.Lock))
            Unlock();

        if (_unlockStatus.Length > 0)
            card.Text(Card.Padding, top + 52 + 56, 14, Chrome.Clip(Fonts.Main, 14, _unlockStatus, width - 150), Palette.Muted);

        if (_unlockUrl != null && Widgets.Button(ui, "channels-open", left + width - Card.Padding - 110, TopMargin + top + 52 + 40, "Open it", ButtonKind.Subtle, out _))
            shell.OpenForge(_unlockUrl);

        return TopMargin + height;
    }

    private void DrawCodeBoxes(Ui ui, double x, double y, double width)
    {
        var box = (width - 4 * 22) / 5;

        for (var i = 0; i < 5; i++)
        {
            var text = _codeParts[i];
            ui.Pos(x + i * (box + 22), y);
            ui.Size(box, 38);

            if (ui.InputEx("##channel-code-" + i, ref text, GoomeSharp.InputFlags.None, "XXXXX", 40) > 0)
                TakeCodePart(i, text);

            if (i == 4)
                continue;

            GoomeSharp.Item dash = Chrome.Hit(ui, "channel-dash-" + i, x + i * (box + 22) + box, y, 22, 38);
            Chrome.Text(ui.Canvas()!, Fonts.Main, 16, dash.X + 6, dash.Y + 19, "–", Palette.Muted);
        }
    }

    private void TakeCodePart(int index, string typed)
    {
        var cleaned = Regex.Replace(typed.ToUpperInvariant(), "[^A-Z0-9]", "");

        if (cleaned.Length >= 25)
        {
            for (var i = 0; i < 5; i++)
                _codeParts[i] = cleaned.Substring(i * 5, 5);

            return;
        }

        _codeParts[index] = cleaned.Length > 5 ? cleaned[..5] : cleaned;
    }

    private void Unlock()
    {
        var code = string.Concat(_codeParts);
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
                Array.Fill(_codeParts, "");
            }
            catch (UserFacingException ex)
            {
                _unlockStatus = ex.Message;
            }

            _unlocking = false;
            shell.Invalidate();
        });
    }
}
