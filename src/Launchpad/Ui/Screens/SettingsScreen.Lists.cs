using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private const string KeysHint = "Keys can publish releases to your mods from scripts or CI. A key is shown once and can't be retrieved later.";

    private string _keyName = "";
    private string? _newToken;
    private bool _creatingKey;

    private double DrawKeys(Ui ui, double left, double width)
    {
        if (Waiting(ui, left, width, out var bottom))
            return bottom;

        var y = TopMargin;
        var hintHeight = Card.HintHeight(KeysHint, width);
        var createHeight = 110 + hintHeight + (_newToken == null ? 0 : 60);
        Card create = Card.Open(ui, "keys-create", left, y, width, createHeight, "Create an access key");
        var rowTop = 44 + create.Hint(0, 44, width, KeysHint) + 8;

        if (_newToken != null)
        {
            create.Bold(Card.Padding, rowTop + 52 + 10, 14, "Copy this key now. It won't be shown again.", Palette.Warn);
            create.Text(Card.Padding, rowTop + 52 + 34, 12.5, Chrome.Clip(Fonts.Mono, 12.5, _newToken, width - 200), Palette.Text, true);
        }

        ui.Pos(left + Card.Padding, y + rowTop);
        ui.Size(width - Card.Padding * 2 - 140, 36);
        ui.InputEx("##key-name", ref _keyName, InputFlags.None, "GitHub Actions", 80);

        if (Card.Button(ui, "Create key", left + width - Card.Padding - 128, y + rowTop, 128) && _keyName.Trim().Length > 0 && !_creatingKey)
            CreateKey();

        if (_newToken != null)
            DrawNewToken(ui, left, y + rowTop + 52, width);

        return DrawKeyList(ui, left, y + createHeight + 16, width);
    }

    private void DrawNewToken(Ui ui, double left, double top, double width)
    {
        if (Card.Button(ui, "Copy", left + width - Card.Padding - 160, top + 18, 70))
        {
            shell.Window.Goome.Clipboard = _newToken!;
            shell.Toast("Key copied.");
        }

        if (Card.Button(ui, "Done", left + width - Card.Padding - 82, top + 18, 70))
            _newToken = null;
    }

    private void CreateKey()
    {
        _creatingKey = true;
        var name = _keyName.Trim();

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await shell.App.Forge.SendJsonAsync(HttpMethod.Post, "/api/v1/api-keys", new { name });
                _newToken = result.Str("token");
                _keyName = "";
                _loadedSection = -1;
            }
            catch (UserFacingException ex)
            {
                shell.Toast(ex.Message);
            }

            _creatingKey = false;
            shell.Invalidate();
        });
    }

    private double DrawKeyList(Ui ui, double left, double y, double width)
    {
        var items = Items();
        var height = 56 + Math.Max(1, items.Count) * 56;
        Card card = Card.Open(ui, "keys-list", left, y, width, height, "Your keys");

        if (items.Count == 0)
            card.Hint(0, 44, width, "No keys yet.");

        for (var i = 0; i < items.Count; i++)
        {
            var key = items[i];
            var top = 52 + i * 56;
            var used = Format.Ago(key.Str("last_used_at"), "") is { Length: > 0 } ago ? "last used " + ago : "never used";
            card.Bold(Card.Padding, top + 14, 14.5, Chrome.Clip(Fonts.Main, 14.5, (key.Str("name") ?? "") + "  " + key.Str("prefix") + "...", width - Card.Padding * 2 - 110), Palette.Text);
            card.Text(Card.Padding, top + 36, 13, used, Palette.Muted);

            if (key.Str("revoked_at") != null)
                card.Text(width - Card.Padding - 56, top + 24, 13.5, "Revoked", Palette.Bad);
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Str("revoked_at") == null && Card.Button(ui, "Revoke", left + width - Card.Padding - 90, y + 52 + i * 56 + 6, 90))
                Send(HttpMethod.Delete, "/api/v1/api-keys/" + Uri.EscapeDataString(items[i].Str("id") ?? ""), null, "", true);
        }

        return y + height;
    }

    private List<JsonNode> Items()
    {
        return (_data?["items"] as JsonArray)?.Where(item => item != null).Select(item => item!).ToList() ?? [];
    }

    private double DrawDevices(Ui ui, double left, double width)
    {
        if (Waiting(ui, left, width, out var bottom))
            return bottom;

        var items = Items();
        var intro = $"Computers where Launchpad can install your injector mods. You can have up to {_data.Long("max_devices")}. Remove one you no longer use to make room.";
        var height = 62 + Card.HintHeight(intro, width) + Math.Max(1, items.Count) * 56;
        Card card = Card.Open(ui, "devices-panel", left, TopMargin, width, height, "Injector devices");
        var top = 44 + card.Hint(0, 44, width, intro) + 8;

        if (items.Count == 0)
            card.Hint(0, top, width, "No devices yet. Sign in to Launchpad to add this computer.");

        for (var i = 0; i < items.Count; i++)
        {
            var used = Format.Ago(items[i].Str("last_used_at"), "") is { Length: > 0 } ago ? "Last used " + ago : "Never used";
            card.Bold(Card.Padding, top + i * 56 + 14, 14.5, Chrome.Clip(Fonts.Main, 14.5, items[i].Str("name") ?? "", width - Card.Padding * 2 - 110), Palette.Text);
            card.Text(Card.Padding, top + i * 56 + 36, 13, used, Palette.Muted);
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (Card.Button(ui, "Remove", left + width - Card.Padding - 90, TopMargin + top + i * 56 + 6, 90))
                Send(HttpMethod.Delete, "/api/v1/injector/devices/" + Uri.EscapeDataString(items[i].Str("id") ?? ""), null, "", true);
        }

        return TopMargin + height;
    }
}
