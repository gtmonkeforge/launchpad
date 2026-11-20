namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SupportScreen
{
    private const string HelpText = "The docs cover installing mods, publishing, payouts and the game loader. If something still looks broken, ask in the community channel where you found MonkeForge, or email the support address shown on the website.";
    private const string DonateText = "MonkeForge is community made and paid for by donations. If it saved you some time, you can chip in on the website. Donations never buy access to anything.";

    private double DrawHelp(Ui ui, double left, double width)
    {
        PageFrame.Title(ui, "support-title", left, 28, "Support", "Get help, or help keep MonkeForge running.");
        var helpHeight = 118 + Card.HintHeight(HelpText, width);
        Card help = Card.Open(ui, "support-help", left, 108, width, helpHeight, "Need a hand?");
        help.Hint(0, 46, width, HelpText);

        if (Widgets.Button(ui, "support-docs", left + Card.Padding, 108 + helpHeight - 54, "Read the docs", ButtonKind.Primary, out var docsWidth, true, 36))
            _shell.Router.Push(new Route(Page.Docs));

        if (Widgets.Button(ui, "support-software", left + Card.Padding + docsWidth + 10, 108 + helpHeight - 54, "Get the apps", ButtonKind.Subtle, out _, true, 36))
            _shell.Router.Push(new Route(Page.Software));

        var donateTop = 108 + helpHeight + 16;
        var donateHeight = 118 + Card.HintHeight(DonateText, width);
        Card donate = Card.Open(ui, "support-donate", left, donateTop, width, donateHeight, "Support MonkeForge");
        donate.Hint(0, 46, width, DonateText);

        if (Widgets.Button(ui, "support-donate-button", left + Card.Padding, donateTop + donateHeight - 54, "Donate on monkeforge.org", ButtonKind.Primary, out _, true, 36))
            _shell.OpenForge("/");

        return donateTop + donateHeight;
    }
}
