using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ManageScreen
{
    private static readonly string[] Visibilities = ["Public · reviewed", "Private · only you", "Requires a channel code"];
    private static readonly string[] VisibilityValues = ["public", "private", "code"];

    private int _visibility;
    private string _slug = "";
    private string _transferTarget = "";

    private void FillAccess(JsonNode mod)
    {
        _visibility = Math.Max(0, Array.IndexOf(VisibilityValues, mod.Str("visibility") ?? "public"));
        _slug = mod.Str("custom_slug") ?? "";
    }

    private double DrawAccess(Ui ui, double width, double y)
    {
        FormKit.Heading(ui, "manage-visibility-head", "Visibility", y);
        y = FormKit.Pair(ui, "manage-visibility", "Who can see this mod?", ref _visibility, Visibilities, y + 34, width / 2);
        y = FormKit.Hint(ui, "Public mods are reviewed. Private and code-only mods don't need approval.", y - 8, width);

        if (FormKit.ActionButton(ui, "manage-visibility-save", "Save visibility", FormKit.Left, y, false, out _, !_busy))
            SaveVisibility();

        y += 56;

        if (_mod!["author"].Bool("is_verified"))
            y = DrawSlug(ui, width, y);

        if (_isOwner)
            y = DrawTransfer(ui, width, y);

        y = DrawTransfers(ui, width, y);
        y = FormKit.Error(ui, _error, y, width);
        return DrawDanger(ui, width, y);
    }

    private void SaveVisibility()
    {
        var value = VisibilityValues[_visibility];

        if (value == "private" && _mod.Str("visibility") == "public")
        {
            _confirm.Ask("Unlist " + _mod.Str("name"), "Only you will be able to see this mod. Players who already installed it keep it, but they won't get updates unless the mod is public again.", "Make private", () => ApplyVisibility(value));
            return;
        }

        ApplyVisibility(value);
    }

    private void ApplyVisibility(string value)
    {
        Run("Visibility saved.", async () =>
        {
            var result = await _shell.App.Forge.SendJsonAsync(HttpMethod.Patch, ModUrl("/visibility"), new { visibility = value });

            if (result.Str("status") == "pending")
                _shell.Toast("Public visibility requested. The mod will be reviewed.");

            await Refresh();
        });
    }

    private double DrawSlug(Ui ui, double width, double y)
    {
        y = FormKit.Field(ui, "manage-slug", "Custom URL (verified benefit)", ref _slug, y, width / 2, "your-mod", "Your mod will also open at /view/ followed by this name.", 40);

        if (FormKit.ActionButton(ui, "manage-slug-save", "Save URL", FormKit.Left, y - 6, false, out _, !_busy))
        {
            var slug = _slug.Trim();
            Run("Custom URL saved.", async () => await _shell.App.Forge.SendJsonAsync(HttpMethod.Patch, ModUrl("/url"), new { custom_slug = slug }));
        }

        return y + 50;
    }

    private double DrawTransfer(Ui ui, double width, double y)
    {
        FormKit.Heading(ui, "manage-transfer-head", "Transfer ownership", y);
        y = FormKit.Hint(ui, "Send this mod to another publisher or a publishing group. They must accept unless you own the destination.", y + 34, width);
        ui.Pos(FormKit.Left, y);
        ui.Size(width - 190, FormKit.RowHeight);
        ui.InputEx("##manage-transfer", ref _transferTarget, InputFlags.None, "@username or ID", 200);

        if (FormKit.ActionButton(ui, "manage-transfer-send", "Request transfer", FormKit.Left + width - 180, y + 1, false, out _, !_busy && _transferTarget.Trim().Length > 0))
            AskTransfer();

        return y + 62;
    }

    private void AskTransfer()
    {
        var target = _transferTarget.Trim();

        _confirm.Ask("Transfer " + _mod.Str("name"), "Transfer ownership to " + target + "? You may lose access to manage this mod.", "Transfer", () => Run("Transfer requested.", async () =>
        {
            var result = await _shell.App.Forge.SendJsonAsync(HttpMethod.Post, ModUrl("/transfer"), new { target_id = target });

            if (result.Str("status") == "accepted")
                _shell.Toast("Ownership transferred.");

            _transferTarget = "";
            await Refresh();
        }));
    }

    private double DrawTransfers(Ui ui, double width, double y)
    {
        for (var i = 0; i < _transfers.Count; i++)
        {
            var transfer = _transfers[i]!;
            var id = transfer.Str("id") ?? "";
            Item row = Chrome.Hit(ui, "manage-transfer-row-" + i, FormKit.Left, y, width, 44);
            Chrome.Box(ui.Canvas()!, row.X, row.Y, width, 44, Palette.Radius, Palette.Surface, Palette.Line);
            var text = transfer.Str("source_name") + " to " + transfer.Str("target_name") + " is waiting";
            Chrome.Text(ui.Canvas()!, Fonts.Main, 13.5, row.X + 14, row.Y + 22, Chrome.Clip(Fonts.Main, 13.5, text, width - 260), Palette.Text);
            var left = FormKit.Left + width - 14;

            foreach (var action in transfer.Bool("incoming") ? new[] { "Accept", "Decline" } : ["Cancel"])
            {
                var buttonWidth = Widgets.ButtonWidth(action);
                left -= buttonWidth;

                if (Widgets.Button(ui, "manage-transfer-" + action + i, left, y + 6, action, ButtonKind.Subtle, out _, !_busy, 32))
                    Run("Transfer updated.", async () =>
                    {
                        await _shell.App.Forge.SendJsonAsync(HttpMethod.Post, "/api/v1/mod-transfers/" + Uri.EscapeDataString(id) + "/" + action.ToLowerInvariant(), new { });
                        await Refresh();
                    });

                left -= 8;
            }

            y += 52;
        }

        return y + 8;
    }

    private double DrawDanger(Ui ui, double width, double y)
    {
        FormKit.Heading(ui, "manage-danger-head", "Remove this mod", y);
        y = FormKit.Hint(ui, "Unlisting hides the mod from everyone but you and keeps its files and history. You can make it public again later.", y + 34, width);

        if (Widgets.Button(ui, "manage-unlist", FormKit.Left, y, "Unlist this mod", ButtonKind.Danger, out var unlistWidth, !_busy && _mod.Str("visibility") != "private", 36))
            _confirm.Ask("Unlist " + _mod.Str("name"), "Only you will be able to see this mod. Players who already installed it keep it, but they won't get updates.", "Unlist", () => ApplyVisibility("private"));

        if (_shell.IsAdmin && Widgets.Button(ui, "manage-delete", FormKit.Left + unlistWidth + 10, y, "Delete permanently", ButtonKind.Danger, out _, !_busy, 36))
            _confirm.Ask("Delete " + _mod.Str("name"), "Delete this mod and all of its releases for good? This can't be undone.", "Delete mod", DeleteMod);

        return y + 50;
    }

    private void DeleteMod()
    {
        Run("Mod deleted.", async () => await _shell.App.Forge.SendJsonAsync(HttpMethod.Delete, "/api/v1/admin/mods/" + Uri.EscapeDataString(_guid), null), () => _shell.Router.Push(new Route(Page.Library)));
    }
}
