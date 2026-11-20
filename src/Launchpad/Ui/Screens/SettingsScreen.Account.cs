using System.Net.Http.Headers;
using System.Text.Json;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SettingsScreen
{
    private string _publicName = "";
    private string _bio = "";
    private string _avatarPath = "";
    private bool _profileFilled;
    private bool _savingProfile;

    private double DrawAccount(Ui ui, double left, double width)
    {
        if (Waiting(ui, left, width, out var bottom))
            return bottom;

        FillProfile();
        var user = shell.User!;
        var name = user.Str("display_name") ?? user.Str("name") ?? "?";
        var details = "@" + user.Str("username") + (user.Str("discord_username") is { } discord ? " · Discord @" + discord : " · Email account");
        var y = TopMargin;

        Card profile = Card.Open(ui, "account-profile", left, y, width, 96, "");
        profile.Avatar(shell.Images.Get(user.Str("avatar")), 22, 24, 48, name);
        profile.Bold(84, 36, 16, Chrome.Clip(Fonts.Main, 16, name, width - 84 - 150), Palette.Text);
        profile.Text(84, 60, 13, Chrome.Clip(Fonts.Mono, 13, details, width - 84 - 150), Palette.Muted, true);

        if (Card.Button(ui, "View profile", left + width - 142, y + 30, 120))
            shell.Router.Push(new Route(Page.Profile));

        y = DrawProfileForm(ui, left, y + 112, width);
        y = DrawAccountId(ui, left, y + 16, width, user.Str("id") ?? "");
        y = DrawSessionCard(ui, left, y + 16, width);

        var gone = "Staff delete accounts so your mods and purchases are handled properly. Open a support ticket on the website.";
        var height = 120 + Card.HintHeight(gone, width);

        if (DangerCard(ui, "account-delete", left, y + 16, width, height, "Delete account", gone, "account-delete-open", "Contact support"))
            shell.OpenForge("/support");

        return y + 16 + height;
    }

    private void FillProfile()
    {
        if (_profileFilled)
            return;

        _profileFilled = true;
        _publicName = _data.Str("public_username") ?? _data.Str("username") ?? "";
        _bio = _data.Str("description") ?? "";
    }

    private double DrawProfileForm(Ui ui, double left, double y, double width)
    {
        const double height = 392;
        Card card = Card.Open(ui, "account-edit", left, y, width, height, "Public profile");
        var field = width - Card.Padding * 2;

        card.Text(Card.Padding, 62, 13, "Username", Palette.Muted);
        ui.Pos(left + Card.Padding, y + 76);
        ui.Size(Math.Min(field, 360), 36);
        ui.InputEx("##profile-name", ref _publicName, InputFlags.None, "Username", 32);

        card.Text(Card.Padding, 134, 13, "About you", Palette.Muted);
        ui.Pos(left + Card.Padding, y + 148);
        ui.Size(field, 100);
        ui.TextEdit("##profile-bio", ref _bio, 100, EditFlags.Wrap, 3000);

        card.Text(Card.Padding, 270, 13, "Picture (path to a PNG, JPG or WebP file)", Palette.Muted);
        ui.Pos(left + Card.Padding, y + 284);
        ui.Size(field - 160, 36);
        ui.InputEx("##profile-avatar", ref _avatarPath, InputFlags.None, "/home/you/me.png", 400);

        if (Card.Button(ui, "Remove", left + width - Card.Padding - 144, y + 284, 144))
            SaveProfile(true);

        if (Widgets.Button(ui, "profile-save", left + Card.Padding, y + height - 52, _savingProfile ? "Saving..." : "Save profile", ButtonKind.Primary, out _, !_savingProfile, 34))
            SaveProfile(false);

        return y + height;
    }

    private void SaveProfile(bool removeAvatar)
    {
        var id = shell.User.Str("id") ?? "";
        var name = _publicName.Trim();
        var bio = _bio;
        var avatar = removeAvatar ? "" : _avatarPath.Trim();
        _savingProfile = true;

        _ = Task.Run(async () =>
        {
            try
            {
                var profile = new Dictionary<string, object?> { ["description"] = bio, ["remove_avatar"] = removeAvatar };

                if (name.Length > 0)
                    profile["public_username"] = name;

                var form = new MultipartFormDataContent { { new StringContent(JsonSerializer.Serialize(profile)), "profile" } };

                if (avatar.Length > 0)
                {
                    if (!File.Exists(avatar))
                        throw new UserFacingException("That picture file doesn't exist.");

                    var file = new ByteArrayContent(await File.ReadAllBytesAsync(avatar));
                    file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    form.Add(file, "avatar", Path.GetFileName(avatar));
                }

                using var response = await shell.App.Forge.SendAsync(HttpMethod.Put, "/api/v1/users/" + Uri.EscapeDataString(id), form);
                await shell.App.Forge.RefreshSessionAsync();
                _avatarPath = "";
                shell.Toast(removeAvatar ? "Picture removed." : "Profile saved.");
            }
            catch (UserFacingException ex)
            {
                shell.Toast(ex.Message);
            }

            _savingProfile = false;
            shell.Invalidate();
        });
    }

    private double DrawAccountId(Ui ui, double left, double y, double width, string id)
    {
        var hint = "Share this with mod authors who want to add you to a private release channel.";
        var height = 94 + Card.HintHeight(hint, width);
        Card card = Card.Open(ui, "account-id", left, y, width, height, "Account ID");
        var below = 44 + card.Hint(0, 44, width, hint);
        card.Text(Card.Padding, below + 22, 13.5, id, Palette.Text, true);

        if (Card.Button(ui, "Copy", left + width - 102, y + below + 5, 80))
        {
            shell.Window.Goome.Clipboard = id;
            shell.Toast("Account ID copied.");
        }

        return y + height;
    }

    private double DrawSessionCard(Ui ui, double left, double y, double width)
    {
        var hint = "You stay signed in for 30 days on this computer. Signing out ends this session only. To sign out everywhere, use Sign-in & security.";
        var height = 120 + Card.HintHeight(hint, width);
        Card card = Card.Open(ui, "account-session", left, y, width, height, "Session");
        card.Hint(0, 44, width, hint);

        if (Card.Button(ui, "Sign out", left + Card.Padding, y + height - 56, 110))
        {
            shell.SignOut();
            shell.Router.Push(new Route(Page.Browse));
        }

        return y + height;
    }

    private bool DangerCard(Ui ui, string id, double left, double y, double width, double height, string title, string hint, string buttonId, string label)
    {
        Card card = Card.Open(ui, id, left, y, width, height, title);
        card.Hint(0, 44, width, hint);
        return Widgets.Button(ui, buttonId, left + Card.Padding, y + height - 56, label, ButtonKind.Danger, out _, true, 36);
    }
}
