using GoomeSharp;

namespace Launchpad.Ui.Screens;

using Ui = GoomeSharp.Ui;

public sealed partial class SoftwareScreen
{
    private static readonly (string Title, string Text)[] LaunchpadFeatures =
    [
        ("Mods", "Search MonkeForge, read reviews and install or remove mods. Each mod can follow its own release channel, so you can try a dev build of one mod without changing the rest."),
        ("Mod loaders", "Works with BepInEx 5 and MelonLoader, and makes sure the right version is installed before the game starts."),
        ("Launching", "Finds your game folder and starts Gorilla Tag through Steam. On Linux it can set up Proton for you."),
        ("Safety scans", "Every plugin you install is checked by MonkeShield. You can scan again at any time."),
        ("Friends", "Sign in to see which friends are online and join their rooms."),
        ("Tray", "Start Launchpad with your computer and keep it in the tray."),
    ];

    private static readonly (string Name, string Usage, string Text)[] Commands =
    [
        ("install", "mforge install monkeframes", "Install a mod. Use -r dev to pick a release channel, or -N to skip the checksum check."),
        ("uninstall", "mforge uninstall monkeframes", "Remove a mod."),
        ("upgrade", "mforge upgrade", "Update all of your mods. Takes the same options as install."),
        ("login", "mforge login", "Sign in so you can install private mods and channels."),
        ("logout", "mforge logout", "Sign out."),
        ("select", "mforge select", "Find your game folder in Steam. Use -m to choose it yourself."),
    ];

    private static readonly (string Title, string Text)[] OpenForgeFeatures =
    [
        ("Works with the apps", "Launchpad, mforge and publishing scripts work with any OpenForge server."),
        ("Mods", "Release channels, publishing groups and mod transfers. New releases are scanned by MonkeShield before they go live."),
        ("Community", "Ratings, comments, friends and chat."),
        ("Moderation", "A review queue, bans, verified badges and download stats."),
        ("Your brand", "Use your own name and logo."),
    ];

    private static readonly (string Name, string Text)[] Settings =
    [
        ("PUBLIC_URL", "The site's public address, like https://monkeforge.org."),
        ("DATA_DIR", "Where the database and uploads are stored. Defaults to ./data."),
        ("DISCORD_CLIENT_ID, DISCORD_CLIENT_SECRET", "Your Discord application, used for sign-in."),
        ("ROOT_ADMIN_ID", "The Discord user ID of the main admin."),
        ("COOKIE_SECURE", "Set to true when the site uses HTTPS."),
        ("MONKESHIELD_URL", "The MonkeShield server to scan releases with. Leave empty to turn scanning off."),
        ("MONKESHIELD_BACKFILL", "Scan any releases that were never scanned when the server starts. On by default."),
        ("TRUST_COUNTRY_HEADER, COUNTRY_HEADER", "Read each visitor's country from your proxy, for download stats. Defaults to Cloudflare's CF-IPCountry."),
        ("APP_DOWNLOADS_DIR, CLI_DIR", "Folders served at /app and /cli."),
        ("BRAND_NAME, BRAND_DESCRIPTION", "Your site's name, and the description search engines show."),
        ("BRAND_GAME", "The game your mods are for, used in page titles. Leave empty to omit it."),
        ("BRAND_LOGO, BRAND_FAVICON", "Image files for your logo and browser icon. The icon uses the logo if it isn't set."),
        ("BRAND_TINT_LOGO", "Color the logo with each user's accent color. On by default only for the built-in logo."),
    ];

    private double DrawLaunchpad(Ui ui, double left, double width, double y)
    {
        y = Header(ui, left, width, y, "Launchpad", "Launchpad installs your mods and starts Gorilla Tag. It runs on Windows and Linux, and it's open source.",
        [
            ("Windows", () => _shell.OpenForge("/app/launchpad_setup.exe"), ButtonKind.Primary),
            ("Linux", () => _shell.OpenForge("/app/launchpad.tar.gz"), ButtonKind.Subtle),
            ("Source", () => PageFrame.OpenLink(_shell, "https://github.com/gtmonkeforge/launchpad"), ButtonKind.Subtle),
        ], "v" + _launchpadVersion);

        y = Heading(ui, left, y, "Features");
        return Features(ui, left, width, y, LaunchpadFeatures);
    }

    private double DrawCli(Ui ui, double left, double width, double y)
    {
        y = Header(ui, left, width, y, "mforge", "mforge installs and updates MonkeForge mods from the command line. It's written in Go and released under the MIT license.",
        [
            ("Source", () => PageFrame.OpenLink(_shell, "https://github.com/gtmonkeforge/cli"), ButtonKind.Subtle),
        ]);

        y = Heading(ui, left, y, "Install");
        y = Paragraph(ui, left, width, y, "Windows 10/11. Run this in Command Prompt, not PowerShell.");
        y = CopyField(ui, left, width, y, "curl -sL \"https://monkeforge.org/cli/setup.bat\" | cmd");
        y = Paragraph(ui, left, width, y, "Linux. Installs to ~/.local/share/mforge and adds it to your PATH.");
        y = CopyField(ui, left, width, y, "curl -sL \"https://monkeforge.org/cli/setup.sh\" | bash");
        y = Heading(ui, left, y + 10, "Commands");

        foreach (var command in Commands)
        {
            ui.Pos(left, y);
            ui.Custom(NextId("command"), width, 64, ItemFlags.NoHit, out Item row);
            Canvas c = ui.Canvas()!;
            Chrome.Box(c, row.X, row.Y, width, 64, Palette.Radius, Palette.Surface, Palette.Line);
            Chrome.Text(c, Fonts.Mono, 13, row.X + 18, row.Y + 21, command.Name, Palette.Accent);
            Chrome.Text(c, Fonts.Main, 14, row.X + 120, row.Y + 21, Chrome.Clip(Fonts.Main, 14, command.Text, width - 140), Palette.Text);
            Chrome.Text(c, Fonts.Mono, 12.5, row.X + 120, row.Y + 44, "$ " + command.Usage, Palette.Faint);
            y += 72;
        }

        return y;
    }

    private double DrawOpenForge(Ui ui, double left, double width, double y)
    {
        y = Header(ui, left, width, y, "OpenForge", "OpenForge is the server that runs MonkeForge. It's open source, so you can host your own.",
        [
            ("Releases", () => PageFrame.OpenLink(_shell, "https://github.com/gtmonkeforge/openforge/releases"), ButtonKind.Primary),
            ("Source", () => PageFrame.OpenLink(_shell, "https://github.com/gtmonkeforge/openforge"), ButtonKind.Subtle),
        ], _forgeVersion == null ? null : "v" + _forgeVersion);

        y = Heading(ui, left, y, "Features");
        y = Features(ui, left, width, y, OpenForgeFeatures);
        y = Heading(ui, left, y, "Deploying");
        y = Paragraph(ui, left, width, y, "1. Download. Get the package for your system from Releases and unpack it. Windows, Linux and macOS are supported, on x64 and ARM. You don't need to install .NET.", Palette.Text);
        y = Terminal(ui, left, width, y, "shell", ["$ mkdir /opt/openforge", "$ tar -xzf openforge-*-linux-x64.tar.gz -C /opt/openforge --strip-components=1"]);
        y = Paragraph(ui, left, width, y, "To build it yourself instead, install the .NET 10 SDK and run ./build.sh from the repository.");
        y = Paragraph(ui, left, width, y + 6, "2. Configure. Set these as environment variables, or put them in a .env file in the folder you start the server from.", Palette.Text);

        foreach (var setting in Settings)
        {
            var lines = Chrome.Wrap(Fonts.Main, 13.5, setting.Text, width - 360, 4);
            var height = Math.Max(44, 22 + lines.Count * 20);
            ui.Pos(left, y);
            ui.Custom(NextId("setting"), width, height, ItemFlags.NoHit, out Item row);
            Canvas c = ui.Canvas()!;
            Chrome.Box(c, row.X, row.Y, width, height, 0, Palette.Surface, Palette.Line);
            var names = Chrome.Wrap(Fonts.Mono, 12, setting.Name, 300, 3);

            for (var i = 0; i < names.Count; i++)
                Chrome.Text(c, Fonts.Mono, 12, row.X + 14, row.Y + 14 + i * 18, names[i], Palette.Text);

            for (var i = 0; i < lines.Count; i++)
                Chrome.Text(c, Fonts.Main, 13.5, row.X + 340, row.Y + 14 + i * 20, lines[i], Palette.Muted);

            y += height - 1;
        }

        y = Paragraph(ui, left, width, y + 16, "3. Run. Put nginx, Caddy or Cloudflare in front of it for HTTPS, and raise the upload limit to 420 MB.", Palette.Text);
        y = Terminal(ui, left, width, y, "shell", ["$ /opt/openforge/OpenForge --urls http://127.0.0.1:8000"]);
        y = Paragraph(ui, left, width, y, "To keep it running with systemd:");

        return Terminal(ui, left, width, y, "/etc/systemd/system/openforge.service",
        [
            "[Unit]",
            "Description=OpenForge",
            "After=network.target",
            "",
            "[Service]",
            "WorkingDirectory=/opt/openforge",
            "ExecStart=/opt/openforge/OpenForge --urls http://127.0.0.1:8000",
            "Restart=always",
            "User=openforge",
            "",
            "[Install]",
            "WantedBy=multi-user.target",
        ]);
    }
}
