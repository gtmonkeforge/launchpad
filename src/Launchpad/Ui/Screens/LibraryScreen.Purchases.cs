using System.Globalization;
using System.Text.Json.Nodes;
using GoomeSharp;
using Launchpad.Infrastructure;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class LibraryScreen
{
    private double DrawPurchases(Ui ui, double left, double y, double inner)
    {
        Item title = Chrome.Hit(ui, "library-purchases-title", left, y, 200, 30);
        Chrome.BoldText(ui.Canvas()!, Fonts.Main, 20, title.X, title.Y + 15, "Purchases", Palette.Text);

        if (shell.SignedIn)
        {
            var label = "Receipts and disputes";
            var width = Fonts.Main.Measure(14, label) + 30;

            if (Widgets.Button(ui, "library-receipts", left + inner - width, y - 2, label, ButtonKind.Subtle, out _))
                shell.OpenForge((shell.User.Str("profile_url") ?? "/settings") + "#purchases");
        }

        y += 46;

        if (!shell.SignedIn)
            return DrawNote(ui, left, y, inner, "Sign in to see the mods you've bought and install them on this computer.", "Sign in");

        if (_purchaseError != null)
            return DrawNote(ui, left, y, inner, _purchaseError, null);

        if (_purchases.Count == 0)
            return DrawNote(ui, left, y, inner, _loading ? "Loading..." : "Mods you buy will show up here.", null);

        foreach (JsonNode item in _purchases)
        {
            DrawPurchase(ui, item, left, y, inner);
            y += RowHeight + 10;
        }

        return y;
    }

    private double DrawNote(Ui ui, double left, double y, double inner, string text, string? button)
    {
        var height = button == null ? 56.0 : 66.0;
        ui.Pos(left, y);
        ui.Custom("library-note-" + text.Length, inner, height, ItemFlags.NoHit, out Item box);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, box.X, box.Y, inner, height, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Chrome.Text(c, Fonts.Main, 14.5, box.X + 20, box.Y + height / 2, text, Palette.Muted);

        if (button != null && Widgets.Button(ui, "library-signin", left + inner - 100, y + 16, button, ButtonKind.Primary, out _))
            shell.SignIn();

        return y + height;
    }

    private void DrawPurchase(Ui ui, JsonNode item, double left, double y, double inner)
    {
        var guid = item.Str("guid") ?? "";
        var name = item.Str("name") ?? guid;
        var available = item.Bool("available");
        var installed = _mods.Any(mod => mod.Guid == guid);

        ui.Pos(left, y);
        ui.Custom("library-purchase-" + guid, inner, RowHeight, ItemFlags.NoHit, out Item row);
        Canvas c = ui.Canvas()!;
        Chrome.Box(c, row.X, row.Y, inner, RowHeight, Palette.RadiusLarge, Palette.Surface, Palette.Line);
        Widgets.Tile(c, shell, row.X + 15, row.Y + 15, 46, name, item.Str("logo"));

        if (available)
        {
            Item link = Chrome.Hit(ui, "library-purchase-name-" + guid, left + 76, y + 11, Fonts.Main.Measure(16, name), 24);
            c = ui.Canvas()!;
            Chrome.BoldText(c, Fonts.Main, 16, link.X, link.Y + 12, name, link.Hovered ? Palette.Accent : Palette.Text);

            if (link.Clicked)
                shell.Router.Push(new Route(Page.Mod, guid));
        }
        else
            Chrome.BoldText(c, Fonts.Main, 16, row.X + 76, row.Y + 23, name, Palette.Text);

        DrawPurchasePills(c, item, available, row.X + 76, row.Y + 42);
        var right = left + inner - 15;

        if (installed)
        {
            var width = Fonts.Main.Measure(12.5, "Installed") + 18;
            Widgets.Pill(c, row.X + inner - 15 - width, row.Y + 26, "Installed", Palette.Good);
        }
        else if (available)
        {
            var busy = _busy.Contains(guid);
            var label = busy ? "Working..." : "Install";
            right -= Fonts.Main.Measure(14, label) + 30;

            if (Widgets.Button(ui, "library-install-" + guid, right, y + 21, label, ButtonKind.Primary, out _, !busy))
                Install(guid, null, name, false);
        }
    }

    private static void DrawPurchasePills(Canvas c, JsonNode item, bool available, double x, double y)
    {
        if (item.Long("amount_cents") > 0)
            x += Widgets.Pill(c, x, y, Format.Money(item.Long("amount_cents"), item.Str("currency") ?? "usd"), Palette.Muted, true) + 5;

        if (DateTimeOffset.TryParse(item.Str("purchased_at"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
            x += Widgets.Pill(c, x, y, when.ToString("MMM d, yyyy", CultureInfo.InvariantCulture), Palette.Muted) + 5;

        if (item.Bool("uses_injector"))
            x += Widgets.Pill(c, x, y, "Injector", Palette.Muted) + 5;

        if (item.Str("dispute_status") is { Length: > 0 } dispute)
            x += Widgets.Pill(c, x, y, "Dispute " + dispute, Palette.Warn) + 5;

        if (!available)
            Widgets.Pill(c, x, y, "No longer listed", Palette.Bad);
    }
}
