using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private const string PasskeyHint = "Sign in with your fingerprint, face, screen lock or a security key. Passkeys need your browser to register, so adding one opens monkeforge.org. You can rename and remove them here.";
    private const string EverywhereHint = "Ends every session on other browsers and apps, including the website and the CLI. This computer stays signed in. Access keys keep working; revoke them under Access keys. MonkeForge doesn't list individual sessions, so this is the way to end them.";

    private string _newEmail = "";
    private string _emailPassword = "";
    private string _currentPassword = "";
    private string _newPassword = "";
    private bool _renameOpen;
    private string _renameId = "";
    private string _renameText = "";
    private bool _confirmOpen;
    private string _confirmTitle = "";
    private string _confirmText = "";
    private string _confirmButton = "";
    private Action? _confirmAction;

    private double DrawSecurity(Ui ui, double left, double width)
    {
        if (Waiting(ui, left, width, out var bottom))
            return bottom;

        var provider = _data.Str("provider") == "email" ? "This is an email account. Sign in with your username or email and password, or a passkey." : "This account signs in with Discord (@" + _data.Str("discord_username") + ") or a passkey.";
        var y = TopMargin;
        Card how = Card.Open(ui, "security-how", left, y, width, 84 + Card.HintHeight(provider, width) - 21, "How you sign in");
        how.Hint(0, 44, width, provider);
        y += 84 + Card.HintHeight(provider, width) - 21 + 16;
        y = DrawEmailCard(ui, left, y, width) + 16;

        if (_data.Bool("has_password"))
            y = DrawPasswordCard(ui, left, y, width) + 16;

        y = DrawPasskeys(ui, left, y, width) + 16;
        return DrawEverywhere(ui, left, y, width);
    }

    private double DrawEmailCard(Ui ui, double left, double y, double width)
    {
        var email = _data.Str("email");
        var pending = _data.Str("pending_email");
        var needsPassword = _data.Bool("has_password");
        var height = 150 + (pending != null ? 28 : 0) + (needsPassword ? 52 : 0);
        Card card = Card.Open(ui, "security-email", left, y, width, height, "Email address");
        card.Text(Card.Padding, 62, 14, email ?? "No confirmed address yet.", email == null ? Palette.Muted : Palette.Text);

        if (email != null)
            card.PillRight(width - Card.Padding, 51, "Confirmed", Palette.Good);

        var top = 80.0;

        if (pending != null)
        {
            card.Text(Card.Padding, top + 8, 13.5, "We sent a link to " + pending + ". Open it to finish changing your address.", Palette.Warn);
            top += 28;
        }

        if (!_data.Bool("mail_configured"))
        {
            card.Text(Card.Padding, top + 14, 13.5, "Email isn't set up on this server yet.", Palette.Muted);
            return y + height;
        }

        ui.Pos(left + Card.Padding, y + top);
        ui.Size(width - Card.Padding * 2 - 190, 36);
        ui.InputEx("##new-email", ref _newEmail, InputFlags.None, email != null ? "New email" : "Email", 254);
        top += 44;

        if (needsPassword)
        {
            ui.Pos(left + Card.Padding, y + top);
            ui.Size(width - Card.Padding * 2 - 190, 36);
            ui.Password("##email-password", ref _emailPassword, "Current password");
            top += 44;
        }

        if (Widgets.Button(ui, "email-send", left + width - Card.Padding - 180, y + 80 + (pending != null ? 28 : 0), "Send confirmation link", ButtonKind.Primary, out _, _newEmail.Trim().Length > 3, 36))
            ChangeEmail();

        return y + height;
    }

    private void ChangeEmail()
    {
        var body = new Dictionary<string, string> { ["email"] = _newEmail.Trim() };

        if (_data.Bool("has_password"))
            body["password"] = _emailPassword;

        _emailPassword = "";
        Send(HttpMethod.Put, "/api/v1/account/email", body, "Check your inbox for a confirmation link.", true);
        _newEmail = "";
    }

    private double DrawPasswordCard(Ui ui, double left, double y, double width)
    {
        const double height = 196;
        Card card = Card.Open(ui, "security-password", left, y, width, height, "Password");
        ui.Pos(left + Card.Padding, y + 56);
        ui.Size(Math.Min(width - Card.Padding * 2, 360), 36);
        ui.Password("##current-password", ref _currentPassword, "Current password");
        ui.Pos(left + Card.Padding, y + 100);
        ui.Size(Math.Min(width - Card.Padding * 2, 360), 36);
        ui.Password("##new-password", ref _newPassword, "New password, at least 8 characters");
        card.Text(Card.Padding, 166, 13, "Changing it signs you out on every other device.", Palette.Muted);

        if (Widgets.Button(ui, "password-change", left + width - Card.Padding - 150, y + 148, "Change password", ButtonKind.Primary, out _, _currentPassword.Length > 0 && _newPassword.Length >= 8, 36))
        {
            Send(HttpMethod.Put, "/api/v1/account/password", new Dictionary<string, string> { ["current_password"] = _currentPassword, ["password"] = _newPassword }, "Password changed. Other devices were signed out.", false);
            _currentPassword = "";
            _newPassword = "";
        }

        return y + height;
    }

    private double DrawPasskeys(Ui ui, double left, double y, double width)
    {
        var keys = ListOf(_data, "passkeys");
        var hint = Card.HintHeight(PasskeyHint, width);
        var height = 66 + hint + Math.Max(1, keys.Count) * 56;
        Card card = Card.Open(ui, "security-passkeys", left, y, width, height, "Passkeys");
        var top = 44 + card.Hint(0, 44, width, PasskeyHint) + 8;

        if (keys.Count == 0)
            card.Hint(0, top, width, "No passkeys yet.");

        for (var i = 0; i < keys.Count; i++)
        {
            var used = Format.Ago(keys[i].Str("last_used_at"), "") is { Length: > 0 } ago ? "last used " + ago : "never used";
            card.Bold(Card.Padding, top + i * 56 + 14, 14.5, Chrome.Clip(Fonts.Main, 14.5, keys[i].Str("name") ?? "Passkey", width - 330), Palette.Text);
            card.Text(Card.Padding, top + i * 56 + 36, 13, "Added " + Format.Date(keys[i].Str("created_at")) + " · " + used, Palette.Muted);
        }

        for (var i = 0; i < keys.Count; i++)
            DrawPasskeyButtons(ui, keys[i], left + width - Card.Padding, y + top + i * 56 + 6);

        if (Widgets.Button(ui, "passkey-add", left + width - Card.Padding - Widgets.ButtonWidth("Add a passkey"), y + 12, "Add a passkey", ButtonKind.Subtle, out _, true, 32))
            shell.OpenForge("/settings/security");

        return y + height;
    }

    private void DrawPasskeyButtons(Ui ui, JsonNode key, double right, double top)
    {
        var id = key.Str("id") ?? "";
        right -= Widgets.ButtonWidth("Remove");

        if (Widgets.Button(ui, "passkey-remove-" + id, right, top, "Remove", ButtonKind.Danger, out _, true, 34))
            Ask("Remove this passkey?", "You won't be able to sign in with it any more. Also delete it from your device or password manager.", "Remove", () => Send(HttpMethod.Delete, "/api/v1/passkeys/" + Uri.EscapeDataString(id), null, "Passkey removed.", true));

        right -= Widgets.ButtonWidth("Rename") + 8;

        if (Widgets.Button(ui, "passkey-rename-" + id, right, top, "Rename", ButtonKind.Subtle, out _, true, 34))
        {
            _renameId = id;
            _renameText = key.Str("name") ?? "";
            _renameOpen = true;
        }
    }

    private double DrawEverywhere(Ui ui, double left, double y, double width)
    {
        var height = 120 + Card.HintHeight(EverywhereHint, width);
        Card card = Card.Open(ui, "security-everywhere", left, y, width, height, "Sign out everywhere");
        card.Hint(0, 44, width, EverywhereHint);

        if (Widgets.Button(ui, "sign-out-others", left + Card.Padding, y + height - 56, "Sign out other devices", ButtonKind.Danger, out _, true, 36))
            Ask("Sign out other devices?", "Every other browser and app is signed out. This computer stays signed in.", "Sign out others", SignOutOthers);

        return y + height;
    }

    private void SignOutOthers()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await shell.App.Forge.SendJsonAsync(HttpMethod.Post, "/api/v1/account/sign-out-everywhere", null);
                await shell.App.Forge.RefreshSessionAsync();
                shell.Toast("Other devices were signed out.");
            }
            catch (UserFacingException ex)
            {
                shell.Toast(ex.Message);
            }
        });
    }

    private void Ask(string title, string text, string button, Action action)
    {
        _confirmTitle = title;
        _confirmText = text;
        _confirmButton = button;
        _confirmAction = action;
        _confirmOpen = true;
    }

    private void DrawSecurityDialogs(Ui ui)
    {
        if (ui.ModalBegin("Name this passkey", ref _renameOpen))
        {
            ui.InputEx("##passkey-name", ref _renameText, InputFlags.None, "Passkey", 60);
            ui.Space(6);

            if (ui.Button("Save") && _renameText.Trim().Length > 0)
            {
                Send(HttpMethod.Patch, "/api/v1/passkeys/" + Uri.EscapeDataString(_renameId), new Dictionary<string, string> { ["name"] = _renameText.Trim() }, "", true);
                ui.PopupClose();
            }

            ui.SameLine();

            if (ui.Button("Cancel"))
                ui.PopupClose();

            ui.ModalEnd();
        }

        if (!ui.ModalBegin(_confirmTitle, ref _confirmOpen))
            return;

        ui.Wrapped(_confirmText);
        ui.Space(6);

        if (ui.Button(_confirmButton))
        {
            _confirmAction?.Invoke();
            ui.PopupClose();
        }

        ui.SameLine();

        if (ui.Button("Cancel"))
            ui.PopupClose();

        ui.ModalEnd();
    }
}
