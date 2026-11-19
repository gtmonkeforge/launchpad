import { app, esc, icon, setTitle, toast } from '/static/js/core.js';
import { appState, call, loaderNames } from '/_launchpad/bridge.js';

const sections = [
    ['game', 'Game folder', 'game'],
    ['loader', 'Mod loader', 'layers'],
    ['about', 'About Launchpad', 'rocket'],
];

export async function render(section = 'game') {
    if (!sections.some(([key]) => key === section))
        section = 'game';

    setTitle('Launchpad settings');
    app.innerHTML = `
        <div class="page-head"><div><h1>Launchpad</h1><p>These settings are stored on this computer.</p></div></div>
        <div class="settings-layout">
            <nav class="side-nav surface" aria-label="Launchpad settings">
                ${sections.map(([key, label, glyph]) => `<a href="/launchpad/${key}" ${key === section ? 'aria-current="page"' : ''}>${icon(glyph)}${label}</a>`).join('')}
                <a href="/settings">${icon('settings')}Account settings</a>
                <a href="/settings/devices">${icon('server')}Devices</a>
            </nav>
            <div class="stack" id="settings-body"></div>
        </div>`;

    const body = app.querySelector('#settings-body');
    const draw = async () => renderers[section](body, await appState(true));
    window.addEventListener('launchpad:mods', () => draw().catch(() => {}));
    await draw();
}

const renderers = {
    async game(body, state) {
        const game = state.game;
        const platformIcon = platform => platform === 'meta' ? 'globe' : 'game';

        body.innerHTML = `
            <section class="surface panel">
                <h2>${icon('game')} Gorilla Tag</h2>
                ${game.valid
                    ? `<div class="install-option" aria-current="true">${icon(platformIcon(game.platform))}<div><strong>${esc(game.label)}</strong><small>${esc(game.path)}</small></div><span class="pill good">${icon('check')}Selected</span></div>`
                    : '<div class="notice">Launchpad hasn’t found Gorilla Tag yet. Pick one of the installs below, or choose the folder that contains <code>Gorilla Tag.exe</code>.</div>'}
            </section>
            <section class="surface panel">
                <div class="row"><h2>${icon('search')} Detected installs</h2><span class="spacer"></span><button class="button subtle small" id="rescan">${icon('refresh')}Scan again</button></div>
                <p class="hint">Launchpad looks in every Steam library (SteamVR)${state.os === 'windows' ? ' and every Meta Horizon Link library' : ''} on this computer.</p>
                <div class="install-list" id="installs"><p class="hint">Scanning…</p></div>
            </section>
            <section class="surface panel">
                <h2>${icon('tray')} Somewhere else?</h2>
                <p class="hint">Choose the folder that contains <code>Gorilla Tag.exe</code>.</p>
                <button class="button subtle" id="browse-folder">${icon('tray')}Choose folder…</button>
            </section>`;

        const list = body.querySelector('#installs');

        const select = async (button, path) => {
            if (button)
                button.disabled = true;

            try {
                await call('game.select', { path });
                toast('Game folder saved.');
                renderers.game(body, await appState(true));
            } catch (error) {
                toast(error.message, 'error');

                if (button)
                    button.disabled = false;
            }
        };

        const scan = async () => {
            list.innerHTML = '<p class="hint">Scanning…</p>';

            try {
                const installs = await call('game.detect');
                list.innerHTML = installs.length
                    ? installs.map(install => `<div class="install-option" ${install.path === game.path ? 'aria-current="true"' : ''}>${icon(platformIcon(install.platform))}<div><strong>${esc(install.label)}</strong><small>${esc(install.path)}</small></div>
                        ${install.path === game.path ? `<span class="pill good">${icon('check')}Selected</span>` : `<button class="button small" data-use="${esc(install.path)}">Use this</button>`}</div>`).join('')
                    : '<p class="hint">No installs found in Steam or Meta Horizon Link libraries.</p>';
                list.querySelectorAll('[data-use]').forEach(button => button.onclick = () => select(button, button.dataset.use));
            } catch (error) {
                list.innerHTML = `<div class="error">${esc(error.message)}</div>`;
            }
        };

        body.querySelector('#rescan').onclick = scan;
        body.querySelector('#browse-folder').onclick = async () => {
            const path = await call('game.browse').catch(error => toast(error.message, 'error'));

            if (path)
                await select(null, path);
        };

        await scan();
    },

    loader(body, state) {
        const loader = state.settings.loader;
        const status = state.loader;

        body.innerHTML = `
            <section class="surface panel">
                <h2>${icon('layers')} Mod loader</h2>
                <p class="hint">Mods are installed for one loader at a time. Switching keeps both loaders’ mods; only the chosen one runs.</p>
                <div class="segmented" role="tablist" aria-label="Mod loader">
                    ${Object.entries(loaderNames).map(([id, name]) => `<button role="tab" data-loader="${id}" aria-selected="${id === loader}">${name}</button>`).join('')}
                </div>
            </section>
            <section class="surface panel">
                <h2>${icon('server')} Status</h2>
                ${!state.game.valid ? '<p class="hint">Choose your game folder first.</p>' : state.loader_error ? `<div class="error">${esc(state.loader_error)}</div>` : `
                <div class="status-grid">
                    <div><span>Runtime</span><strong>${status?.runtime_installed ? 'Installed' + (status.version ? ' · ' + esc(status.version) : '') : 'Not installed'}</strong></div>
                    <div><span>Active</span><strong>${status?.active ? 'Yes' : 'No'}</strong></div>
                    <div><span>Injector plugin</span><strong>${status?.injector_deployed ? 'Installed' : 'Not needed yet'}</strong></div>
                </div>
                <p class="hint">${loaderNames[loader]} is installed automatically with your first mod. Use this to reinstall it or get the newest release from its official GitHub page.</p>
                <button class="button subtle" id="install-loader">${icon('download')}${status?.runtime_installed ? 'Reinstall' : 'Install'} ${loaderNames[loader]}</button>`}
            </section>
            ${state.os === 'linux' ? `<section class="surface panel">
                <h2>${icon('terminal')} Proton</h2>
                <p class="hint">Under Proton, the loader only starts if Steam’s launch options tell Wine to use it. Launchpad does this when it installs the loader. Close Steam first, or Steam may undo the change.</p>
                <button class="button subtle" id="proton">${icon('tool')}Set up launch options</button>
            </section>` : ''}`;

        const busy = async (button, work) => {
            button.disabled = true;

            try {
                await work();
            } catch (error) {
                toast(error.message, 'error');
            } finally {
                if (button.isConnected)
                    button.disabled = false;
            }
        };

        body.querySelectorAll('[data-loader]').forEach(button => button.onclick = () => busy(button, async () => {
            if (button.dataset.loader === loader)
                return;

            await call('loader.set', { loader: button.dataset.loader });
            toast(`Switched to ${loaderNames[button.dataset.loader]}.`);
            renderers.loader(body, await appState(true));
        }));

        body.querySelector('#install-loader')?.addEventListener('click', event => busy(event.currentTarget, async () => {
            const result = await call('loader.install');
            toast(`${loaderNames[loader]} ${result.version} is installed.`);
            renderers.loader(body, await appState(true));
        }));

        body.querySelector('#proton')?.addEventListener('click', event => busy(event.currentTarget, async () => {
            const result = await call('proton.configure');
            toast(result.updated ? `Updated launch options for ${result.updated} Steam ${result.updated === 1 ? 'account' : 'accounts'}.` : 'Launch options were already set up.');
        }));
    },

    about(body, state) {
        body.innerHTML = `
            <section class="surface panel">
                <h2>${icon('rocket')} MonkeForge Launchpad</h2>
                <dl class="details-list">
                    <div><dt>Version</dt><dd class="mono">${esc(state.version)}</dd></div>
                    <div><dt>Server</dt><dd class="mono">${esc(state.server)}</dd></div>
                    <div><dt>Platform</dt><dd>${state.os === 'windows' ? 'Windows' : 'Linux'}</dd></div>
                    <div><dt>Sign-in kept in</dt><dd>${esc(state.secrets?.description || '')}</dd></div>
                    ${state.device ? `<div><dt>This device</dt><dd>${esc(state.device.name)} <span class="hint">(${esc(state.device.storage)})</span></dd></div>` : ''}
                </dl>
                <p class="hint">Launchpad is the desktop companion for MonkeForge. Community made. Not affiliated with Another Axiom.</p>
                <div class="row"><a class="button subtle small" href="/software/launchpad">${icon('rocket')}Launchpad on MonkeForge</a><a class="button subtle small" href="/docs">${icon('note')}Docs</a></div>
            </section>`;
    },
};

