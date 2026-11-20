using System.Text.Json.Nodes;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ManageScreen
{
    private int _channelPick = -1;
    private string _channelName = "";
    private string _channelNotes = "";
    private bool _channelPublic = true;
    private bool _channelEnabled = true;
    private string _channelMembers = "";
    private string _shareCode = "";

    private bool NewChannel => _channelPick >= _channels.Count;

    private JsonNode? SelectedChannel => NewChannel ? null : _channels[_channelPick];

    private void FillChannel()
    {
        var channel = SelectedChannel;
        _channelName = "";
        _channelNotes = channel.Str("notes") ?? "";
        _channelPublic = channel == null || channel.Bool("public");
        _channelEnabled = channel == null || channel.Bool("enabled");
        _channelMembers = channel?["members"] is JsonArray members ? string.Join(", ", members.Select(member => member?.ToString())) : "";
        _shareCode = "";
    }

    private double DrawChannels(Ui ui, double width, double y)
    {
        if (_channelPick < 0)
        {
            _channelPick = Math.Max(0, _channels.ToList().FindIndex(channel => channel.Str("name") == "stable"));
            FillChannel();
        }

        var names = _channels.Select(channel => channel.Str("name") ?? "").Append("+ New channel...").ToArray();
        y = FormKit.Label(ui, "Channel (" + _channels.Count + "/20)", y);
        ui.Pos(FormKit.Left, y);
        ui.Size(width / 2, FormKit.RowHeight);

        if (ui.Combo("##manage-channel", ref _channelPick, names))
            FillChannel();

        y += FormKit.RowHeight + 16;

        if (NewChannel)
            y = FormKit.Field(ui, "channel-name", "Name", ref _channelName, y, width / 2, "beta", "Lowercase letters, digits and hyphens, starting with a letter.", 40);

        y = FormKit.TextArea(ui, "channel-notes", "Notes", ref _channelNotes, y, width, 80, null, 10000);
        ui.Pos(FormKit.Left, y);
        ui.Checkbox("Public", ref _channelPublic);
        ui.Pos(FormKit.Left + 150, y);

        if (SelectedChannel.Str("name") == "stable")
            _channelEnabled = true;
        else
            ui.Checkbox("Enabled", ref _channelEnabled);

        y += 42;

        if (!_channelPublic)
            y = FormKit.Field(ui, "channel-members", "Members (MonkeForge user IDs)", ref _channelMembers, y, width, "Comma-separated IDs", null, 4000);

        y = FormKit.Error(ui, _error, y, width);

        if (FormKit.ActionButton(ui, "manage-channel-save", "Save channel", FormKit.Left, y, true, out var saveWidth, !_busy))
            SaveChannel();

        if (!NewChannel && FormKit.ActionButton(ui, "manage-channel-share", "Share code", FormKit.Left + saveWidth + 10, y, false, out _, !_busy))
            ShareChannel();

        y += 48;

        if (_shareCode.Length > 0)
            y = DrawShareCode(ui, width, y);

        return y;
    }

    private double DrawShareCode(Ui ui, double width, double y)
    {
        ui.Pos(FormKit.Left, y);
        ui.Size(width - 110, FormKit.RowHeight);
        ui.InputEx("##manage-share-code", ref _shareCode, GoomeSharp.InputFlags.ReadOnly, null, 200);

        if (FormKit.ActionButton(ui, "manage-share-copy", "Copy", FormKit.Left + width - 100, y + 1))
        {
            _shell.Window.Goome.Clipboard = _shareCode;
            _shell.Toast("Code copied.");
        }

        return y + 56;
    }

    private void SaveChannel()
    {
        var channel = SelectedChannel;
        var payload = new JsonObject
        {
            ["notes"] = _channelNotes,
            ["public"] = _channelPublic,
            ["enabled"] = _channelEnabled,
            ["members"] = new JsonArray(_channelMembers.Split([',', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(member => (JsonNode)JsonValue.Create(member)!).ToArray()),
        };

        if (channel == null)
            payload["name"] = _channelName.Trim();

        var name = channel.Str("name");

        Run("Channel saved.", async () =>
        {
            var path = ModUrl("/channels" + (channel == null ? "" : "/" + Uri.EscapeDataString(name!)));
            await _shell.App.Forge.SendJsonAsync(channel == null ? HttpMethod.Post : HttpMethod.Patch, path, payload);
            await Refresh();
            _channelPick = -1;
        });
    }

    private void ShareChannel()
    {
        var name = SelectedChannel.Str("name") ?? "stable";

        Run("Share code ready.", async () =>
        {
            var result = await _shell.App.Forge.SendJsonAsync(HttpMethod.Post, ModUrl("/channels/" + Uri.EscapeDataString(name) + "/share-code"), new { });
            _shareCode = result.Str("code") ?? "";
        });
    }
}
