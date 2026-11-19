import { app, bindCopy, esc, icon, setTitle } from '../core.js';

const repos = {
    launchpad: 'https://github.com/gtmonkeforge/launchpad',
    cli: 'https://github.com/gtmonkeforge/cli',
    openforge: 'https://github.com/gtmonkeforge/openforge',
};

const installCommands = {
    windows: 'curl -sL "https://monkeforge.org/cli/setup.bat" | cmd',
    linux: 'curl -sL "https://monkeforge.org/cli/setup.sh" | bash',
};

export function render(product) {
    const pages = { launchpad, cli, openforge };
    const page = pages[product] || hub;

    page();
    bindCopy(app);
}

function tabs(active) {
    const items = [['', 'All software'], ['launchpad', 'Launchpad'], ['cli', 'mforge'], ['openforge', 'OpenForge']];
    return `<nav class="segmented software-tabs" aria-label="Software">${items.map(([key, label]) => `<a href="/software${key ? '/' + key : ''}" ${key === active ? 'aria-current="page"' : ''}>${label}</a>`).join('')}</nav>`;
}

function header(title, text, actions = '') {
    return `<header class="product-head"><h1>${title}</h1><p>${text}</p>${actions ? `<div class="row">${actions}</div>` : ''}</header>`;
}

function features(items) {
    return `<dl class="feature-list">${items.map(([title, text]) => `<div><dt>${title}</dt><dd>${text}</dd></div>`).join('')}</dl>`;
}

function terminal(title, lines) {
    return `<div class="terminal"><header><span>${esc(title)}</span></header><pre>${lines}</pre></div>`;
}

function copyCommand(command, label) {
    return `<div class="copy-field"><input readonly value="${esc(command)}" aria-label="${esc(label)}" spellcheck="false"><button class="button subtle" data-copy="${esc(command)}" data-copy-label="Command copied.">${icon('copy')}Copy</button></div>`;
}

function hub() {
    setTitle('Software');
    const products = [
        ['/software/launchpad', 'Launchpad', 'Desktop app for installing mods and launching Gorilla Tag.', 'Windows, Linux'],
        ['/software/cli', 'mforge', 'Command-line tool for installing and upgrading mods.', 'Windows, Linux'],
        ['/software/openforge', 'OpenForge', 'The server behind this site. Open source, written in C#.', 'Self-hosted'],
    ];

    app.innerHTML = `${tabs('')}
        ${header('Software', 'Tools for using MonkeForge outside the browser, and the server that runs it.')}
        <div class="product-list">${products.map(([href, name, text, platforms]) => `<a class="surface product-row" href="${href}"><strong>${name}</strong><span>${text}</span><span class="hint">${platforms}</span>${icon('next')}</a>`).join('')}</div>`;
}

function launchpad() {
    setTitle('Launchpad');
    app.innerHTML = `${tabs('launchpad')}
        ${header('Launchpad', 'Launchpad installs your mods and starts Gorilla Tag. It runs on Windows and Linux, and it’s open source.', `
            <a class="button" href="/app/launchpad_setup.exe">${icon('download')}Windows</a>
            <a class="button subtle" href="/app/launchpad.tar.gz">${icon('download')}Linux</a>
            <a class="button ghost" href="${repos.launchpad}" target="_blank" rel="noopener noreferrer">${icon('github')}Source</a>`)}
        <img class="screenshot" src="/static/launchpad-screenshot.png" width="1284" height="893" alt="Launchpad showing the Gorilla Tag launch screen, installed mods and friends">
        <h2 class="section-heading">Features</h2>
        ${features([
            ['Mods', 'Search MonkeForge, read reviews and install or remove mods. Each mod can follow its own release channel, so you can try a dev build of one mod without changing the rest.'],
            ['Mod loaders', 'Works with BepInEx 5 and MelonLoader, and makes sure the right version is installed before the game starts.'],
            ['Launching', 'Finds your game folder and starts Gorilla Tag through Steam. On Linux it can set up Proton for you.'],
            ['Safety scans', 'Every plugin you install is checked by MonkeShield. You can scan again at any time.'],
            ['Friends', 'Sign in to see which friends are online and join their rooms.'],
            ['Tray', 'Start Launchpad with your computer and keep it in the tray.'],
        ])}`;
}

function cli() {
    setTitle('mforge');
    const commands = [
        ['install', 'mforge install monkeframes', 'Install a mod. Use -r dev to pick a release channel, or -N to skip the checksum check.'],
        ['uninstall', 'mforge uninstall monkeframes', 'Remove a mod.'],
        ['upgrade', 'mforge upgrade', 'Update all of your mods. Takes the same options as install.'],
        ['login', 'mforge login', 'Sign in so you can install private mods and channels.'],
        ['logout', 'mforge logout', 'Sign out.'],
        ['select', 'mforge select', 'Find your game folder in Steam. Use -m to choose it yourself.'],
    ];

    app.innerHTML = `${tabs('cli')}
        ${header('mforge', 'mforge installs and updates MonkeForge mods from the command line. It’s written in Go and released under the MIT license.', `
            <a class="button ghost" href="${repos.cli}" target="_blank" rel="noopener noreferrer">${icon('github')}Source</a>`)}
        <h2 class="section-heading">Install</h2>
        <div class="grid-2">
            <section class="surface panel"><h3>Windows 10/11</h3><p class="hint">Run this in Command Prompt, not PowerShell.</p>${copyCommand(installCommands.windows, 'Windows install command')}</section>
            <section class="surface panel"><h3>Linux</h3><p class="hint">Installs to ~/.local/share/mforge and adds it to your PATH.</p>${copyCommand(installCommands.linux, 'Linux install command')}</section>
        </div>
        <h2 class="section-heading">Commands</h2>
        <div class="table-wrap surface"><table class="data command-table"><tbody>${commands.map(([name, usage, text]) => `<tr><td><code>${name}</code></td><td>${esc(text)}<code class="usage">$ ${esc(usage)}</code></td></tr>`).join('')}</tbody></table></div>`;
}

function openforge() {
    setTitle('OpenForge');
    const env = [
        ['PUBLIC_URL', 'The site’s public address, like https://monkeforge.org.'],
        ['DATA_DIR', 'Where the database and uploads are stored. Defaults to ./data.'],
        ['DISCORD_CLIENT_ID, DISCORD_CLIENT_SECRET', 'Your Discord application, used for sign-in.'],
        ['ROOT_ADMIN_ID', 'The Discord user ID of the main admin.'],
        ['COOKIE_SECURE', 'Set to true when the site uses HTTPS.'],
        ['MONKESHIELD_URL', 'The MonkeShield server to scan releases with. Leave empty to turn scanning off.'],
        ['MONKESHIELD_BACKFILL', 'Scan any releases that were never scanned when the server starts. On by default.'],
        ['TRUST_COUNTRY_HEADER, COUNTRY_HEADER', 'Read each visitor’s country from your proxy, for download stats. Defaults to Cloudflare’s CF-IPCountry.'],
        ['APP_DOWNLOADS_DIR, CLI_DIR', 'Folders served at /app and /cli.'],
        ['BRAND_NAME, BRAND_DESCRIPTION', 'Your site’s name, and the description search engines show.'],
        ['BRAND_GAME', 'The game your mods are for, used in page titles. Leave empty to omit it.'],
        ['BRAND_LOGO, BRAND_FAVICON', 'Image files for your logo and browser icon. The icon uses the logo if it isn’t set.'],
        ['BRAND_TINT_LOGO', 'Color the logo with each user’s accent color. On by default only for the built-in logo.'],
    ];

    app.innerHTML = `${tabs('openforge')}
        ${header('OpenForge', 'OpenForge is the server that runs MonkeForge. It’s open source, so you can host your own.', `
            <a class="button" href="${repos.openforge}/releases" target="_blank" rel="noopener noreferrer">${icon('download')}Releases</a>
            <a class="button ghost" href="${repos.openforge}" target="_blank" rel="noopener noreferrer">${icon('github')}Source</a>`)}
        <h2 class="section-heading">Features</h2>
        ${features([
            ['Works with the apps', 'Launchpad, mforge and publishing scripts work with any OpenForge server.'],
            ['Mods', 'Release channels, publishing groups and mod transfers. New releases are scanned by MonkeShield before they go live.'],
            ['Community', 'Ratings, comments, friends and chat.'],
            ['Moderation', 'A review queue, bans, verified badges and download stats.'],
            ['Your brand', 'Use your own name and logo.'],
        ])}
        <h2 class="section-heading">Deploying</h2>
        <ol class="steps">
            <li><h3>Download</h3><p>Get the package for your system from Releases and unpack it. Windows, Linux and macOS are supported, on x64 and ARM. You don’t need to install .NET.</p>
                ${terminal('shell', `$ mkdir /opt/openforge
$ tar -xzf openforge-*-linux-x64.tar.gz -C /opt/openforge --strip-components=1`)}
                <p>To build it yourself instead, install the .NET 10 SDK and run <code>./build.sh</code> from the repository.</p></li>
            <li><h3>Configure</h3><p>Set these as environment variables, or put them in a <code>.env</code> file in the folder you start the server from.</p>
                <div class="table-wrap surface"><table class="data env-table"><tbody>${env.map(([name, text]) => `<tr><td>${esc(name)}</td><td>${esc(text)}</td></tr>`).join('')}</tbody></table></div></li>
            <li><h3>Run</h3><p>Put nginx, Caddy or Cloudflare in front of it for HTTPS, and raise the upload limit to 420 MB.</p>
                ${terminal('shell', `$ /opt/openforge/OpenForge --urls http://127.0.0.1:8000`)}
                <p>To keep it running with systemd:</p>
                ${terminal('/etc/systemd/system/openforge.service', `[Unit]
Description=OpenForge
After=network.target

[Service]
WorkingDirectory=/opt/openforge
ExecStart=/opt/openforge/OpenForge --urls http://127.0.0.1:8000
Restart=always
User=openforge

[Install]
WantedBy=multi-user.target`)}</li>
        </ol>`;
}
