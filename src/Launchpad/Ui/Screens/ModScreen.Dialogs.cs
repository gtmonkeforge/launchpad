using System.Globalization;
using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class ModScreen
{
    private static readonly int[] DonationPresets = [300, 500, 1000, 2500];

    private bool _donateOpen;
    private bool _reportOpen;
    private int _donateAmount = 500;
    private string _donateCustom = "";
    private string? _donateError;
    private bool _donating;
    private JsonNode? _store;
    private string _reportText = "";

    private void DrawDialogs(Ui ui)
    {
        DrawDeleteDialog(ui);
        DrawDonateDialog(ui);
        DrawReportDialog(ui);
        DrawLightbox(ui);
    }

    private void DrawDeleteDialog(Ui ui)
    {
        if (!ui.ModalBegin("Delete this comment?", ref _deleteOpen))
            return;

        ui.Wrapped("This removes your comment for everyone.");
        ui.Space(6);

        if (ui.Button("Delete"))
        {
            DeleteComment();
            ui.PopupClose();
        }

        ui.SameLine();

        if (ui.Button("Keep it"))
            ui.PopupClose();

        ui.ModalEnd();
    }

    private void OpenDonate()
    {
        _donateOpen = true;
        _donateError = null;
        _donateCustom = "";
        _donateAmount = 500;

        if (_store != null)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                _store = await _shell.App.Forge.GetJsonAsync("/api/v1/store");
            }
            catch (UserFacingException)
            {
                _store = new JsonObject { ["enabled"] = false };
            }

            _shell.Invalidate();
        });
    }

    private static long ToMinor(string text, string currency)
    {
        var cleaned = new string(text.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());

        if (!double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return -1;

        return (long)Math.Round(currency is "jpy" or "krw" ? value : value * 100);
    }

    private void DrawDonateDialog(Ui ui)
    {
        var name = _mod?["author"].Str("name") ?? "";

        if (!ui.ModalBegin("Support " + name, ref _donateOpen))
            return;

        var currency = _store.Str("currency") ?? "usd";
        ui.Width(460);
        ui.Wrapped($"{name} receives the full amount. MonkeForge covers the card processing fee.");
        ui.Space(8);

        if (_store == null)
            ui.Spinner(20);
        else if (!_store.Bool("enabled"))
            ui.Wrapped("Payments aren't set up on this server yet.");
        else if (!_shell.SignedIn)
        {
            if (ui.Button("Sign in to donate"))
            {
                _shell.Router.Push(new Route(Page.SignIn));
                ui.PopupClose();
            }
        }
        else
            DrawDonateForm(ui, currency);

        ui.Space(6);

        if (ui.Button("Cancel"))
            ui.PopupClose();

        ui.ModalEnd();
    }

    private void DrawDonateForm(Ui ui, string currency)
    {
        ui.Label("Amount");

        foreach (var preset in DonationPresets)
        {
            if (ui.Radio(Format.Money(preset, currency), ref _donateAmount, preset))
                _donateCustom = "";

            ui.SameLine();
        }

        ui.Space(4);
        ui.Label("Other amount");
        ui.Width(200);

        if (ui.InputEx("##donate-custom", ref _donateCustom, InputFlags.None, "5.00", 12) != 0 || _donateCustom.Length > 0)
            _donateAmount = 0;

        if (_donateError != null)
            ui.Wrapped(_donateError);

        ui.Space(6);

        if (ui.Button(_donating ? "Opening…" : "Continue to payment") && !_donating)
            StartDonation(currency, ui);

        ui.SameLine();
    }

    private void StartDonation(string currency, Ui ui)
    {
        var minimum = _store.Long("min_donation_cents");
        var maximum = _store.Long("max_donation_cents");
        var amount = _donateCustom.Trim().Length > 0 ? ToMinor(_donateCustom, currency) : _donateAmount;

        if (amount < minimum || amount > maximum)
        {
            _donateError = $"Choose an amount between {Format.Money(minimum, currency)} and {Format.Money(maximum, currency)}.";
            return;
        }

        _donating = true;
        _donateError = null;
        var body = new { recipient = _mod["author"].Str("id"), amount_cents = amount, guid = _mod.Str("guid") };

        _ = Task.Run(async () =>
        {
            try
            {
                var reply = await _shell.App.Forge.SendJsonAsync(HttpMethod.Post, "/api/v1/donations", body);
                _shell.Call("shell.open", new JsonObject { ["url"] = reply.Str("url") });
                _donateOpen = false;
                _shell.Toast("Finish your donation in the browser.");
            }
            catch (UserFacingException ex)
            {
                _donateError = ex.Message;
            }

            _donating = false;
            _shell.Invalidate();
        });
    }

    private void DrawReportDialog(Ui ui)
    {
        if (!ui.ModalBegin("Report this mod", ref _reportOpen))
            return;

        ui.Width(460);
        ui.Wrapped("Tell the MonkeForge staff what is wrong with " + _mod.Str("name")?.Trim() + ". Your report is copied with the mod link so you can send it to them.");
        ui.Space(8);
        ui.Width(460);
        ui.TextEdit("##mod-report", ref _reportText, 110, EditFlags.Wrap, 2000);
        ui.Space(6);

        if (ui.Button("Copy report") && _reportText.Trim().Length > 0)
        {
            var link = new Uri(_shell.App.Forge.BaseUri, "/view/" + Uri.EscapeDataString(_guid));
            _shell.Window.Goome.Clipboard = $"Report for {_mod.Str("name")} ({link})\n\n{_reportText.Trim()}";
            _shell.Toast("Report copied.");
            _reportText = "";
            ui.PopupClose();
        }

        ui.SameLine();

        if (ui.Button("Cancel"))
            ui.PopupClose();

        ui.ModalEnd();
    }
}
