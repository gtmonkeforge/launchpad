import { api, app, auth, date, emptyState, esc, icon, modIcon, money, setTitle, signInUrl, toast } from '/static/js/core.js';
import { appState, call, installedMods, loaderNames } from '/_launchpad/bridge.js';

export async function render() {
    setTitle('Library');
    const state = await appState(true);

    if (!state.game.valid) {
        app.innerHTML = `<div class="page-head"><div><h1>Library</h1></div></div>
            ${emptyState('Choose your game folder', 'Launchpad finds Gorilla Tag in Steam (SteamVR) and Meta Horizon Link libraries automatically, or you can pick the folder yourself.', `<a class="button" href="/launchpad/game">${icon('game')}Choose game folder</a>`)}`;
        return;
    }

    app.innerHTML = `
        <div class="page-head">
            <div><h1>Library</h1><p>${esc(state.game.label || 'Gorilla Tag')} · <span class="mono">${esc(state.game.path)}</span></p></div>
            <div class="row"><button class="button subtle" id="refresh">${icon('refresh')}Check for updates</button><button class="button" id="update-all" hidden>${icon('download')}Update all</button></div>
        </div>
        ${state.game.running ? '<div class="notice">Gorilla Tag is running. Close it before installing or removing mods that use files.</div>' : ''}
        <div id="installed"><div class="page-loading"><span class="spinner"></span>Loading…</div></div>
        <div class="section-title"><h2>Purchases</h2><span class="spacer"></span>${auth.user ? `<a class="button ghost small" href="${esc(auth.user.profile_url)}#purchases">${icon('tray')}Receipts & disputes</a>` : ''}</div>
        <div id="purchases"></div>`;

    let details = {};

    const draw = async () => {
        const mods = await installedMods(true);
        const box = app.querySelector('#installed');
        const loader = (await appState()).settings.loader;

        if (!mods.length) {
            box.innerHTML = emptyState('No mods installed yet', 'Find something you like and install it from its page.', `<a class="button" href="/browse">${icon('grid')}Browse mods</a>`);
            app.querySelector('#update-all').hidden = true;
            await purchases(mods);
            return;
        }

        await Promise.all(mods.filter(m => !details[m.guid]).map(async m => {
            details[m.guid] = await api('/api/v1/mods/' + encodeURIComponent(m.guid) + '?channel=' + encodeURIComponent(m.channel)).catch(() => ({ name: m.name }));
        }));

        const updatable = mods.filter(m => details[m.guid].current_version && details[m.guid].current_version !== m.version && m.loader === loader);
        app.querySelector('#update-all').hidden = updatable.length < 2;

        box.innerHTML = `<div class="library-list">${mods.map(m => {
            const detail = details[m.guid];
            const latest = detail.current_version;
            const inactive = m.loader !== loader;

            return `<article class="surface library-row">
                ${modIcon({ name: m.name, logo: detail.logo })}
                <div>
                    <h3><a href="/view/${esc(encodeURIComponent(m.guid))}${m.channel !== 'stable' ? '?channel=' + esc(encodeURIComponent(m.channel)) : ''}">${esc(detail.name || m.name)}</a></h3>
                    <div class="badges">
                        <span class="pill mono">v${esc(m.version)}</span>
                        <span class="pill">${esc(loaderNames[m.loader] || m.loader)}</span>
                        ${m.channel !== 'stable' ? `<span class="pill accent mono">${esc(m.channel)}</span>` : ''}
                        ${m.injector ? `<span class="pill" title="Loaded into the game from memory while Launchpad is open">${icon('lock')}Injector</span>` : ''}
                        ${latest && latest !== m.version ? `<span class="pill warn">v${esc(latest)} available</span>` : ''}
                        ${inactive ? '<span class="pill bad">Inactive loader</span>' : ''}
                    </div>
                </div>
                <div class="actions">
                    ${latest && latest !== m.version && !inactive ? `<button class="button small" data-update="${esc(m.guid)}">${icon('refresh')}Update</button>` : ''}
                    <button class="button danger small" data-remove="${esc(m.guid)}">${icon('trash')}Remove</button>
                </div>
            </article>`;
        }).join('')}</div>`;

        box.querySelectorAll('[data-update]').forEach(button => button.onclick = () => update(button, mods.find(m => m.guid === button.dataset.update)));
        box.querySelectorAll('[data-remove]').forEach(button => button.onclick = () => remove(button, mods.find(m => m.guid === button.dataset.remove)));
        await purchases(mods);
    };

    const update = async (button, mod) => {
        if (button)
            button.disabled = true;

        try {
            const result = await call('mods.install', { guid: mod.guid, channel: mod.channel });
            toast(`${mod.name} updated to v${result.version}.`);
            return true;
        } catch (error) {
            toast(error.message, 'error');

            if (button)
                button.disabled = false;

            return false;
        }
    };

    const remove = async (button, mod) => {
        if (!confirm(`Remove ${mod.name} from Gorilla Tag?`))
            return;

        button.disabled = true;

        try {
            await call('mods.uninstall', { guid: mod.guid });
            toast(`${mod.name} was removed.`);
        } catch (error) {
            toast(error.message, 'error');
            button.disabled = false;
        }
    };

    app.querySelector('#refresh').onclick = async event => {
        event.currentTarget.disabled = true;
        details = {};
        await draw();
        event.currentTarget.disabled = false;
    };

    app.querySelector('#update-all').onclick = async event => {
        event.currentTarget.disabled = true;
        const loader = (await appState()).settings.loader;

        for (const mod of await installedMods()) {
            const latest = details[mod.guid]?.current_version;

            if (latest && latest !== mod.version && mod.loader === loader && !await update(null, mod))
                break;
        }
    };

    window.addEventListener('launchpad:mods', () => draw().catch(() => {}));
    await draw();
}

async function purchases(installed) {
    const box = app.querySelector('#purchases');

    if (!auth.user) {
        box.innerHTML = `<div class="surface panel"><p class="hint">Sign in to see the mods you’ve bought and install them on this computer.</p><a class="button small" href="${esc(signInUrl('/library'))}">${icon('user')}Sign in</a></div>`;
        return;
    }

    try {
        const { items } = await api('/api/v1/purchases');

        if (!items.length) {
            box.innerHTML = `<div class="surface panel"><p class="hint">Mods you buy will show up here.</p></div>`;
            return;
        }

        box.innerHTML = `<div class="library-list">${items.map(item => `<article class="surface library-row">
            ${modIcon(item)}
            <div>
                <h3>${item.available ? `<a href="/view/${esc(encodeURIComponent(item.guid))}">${esc(item.name)}</a>` : esc(item.name)}</h3>
                <div class="badges">
                    ${item.amount_cents ? `<span class="pill mono">${esc(money(item.amount_cents, item.currency || 'usd'))}</span>` : ''}
                    <span class="pill">${esc(date(item.purchased_at))}</span>
                    ${item.uses_injector ? `<span class="pill">${icon('lock')}Injector</span>` : ''}
                    ${item.dispute_status ? `<span class="pill warn">Dispute ${esc(item.dispute_status)}</span>` : ''}
                    ${item.available ? '' : '<span class="pill bad">No longer listed</span>'}
                </div>
            </div>
            <div class="actions">${installed.some(m => m.guid === item.guid)
                ? `<span class="pill good">${icon('check')}Installed</span>`
                : item.available ? `<button class="button small" data-install="${esc(item.guid)}">${icon('download')}Install</button>` : ''}</div>
        </article>`).join('')}</div>`;

        box.querySelectorAll('[data-install]').forEach(button => button.onclick = async () => {
            button.disabled = true;

            try {
                const result = await call('mods.install', { guid: button.dataset.install });
                toast(`${result.name} is installed.`);
            } catch (error) {
                toast(error.message, 'error');
                button.disabled = false;
            }
        });
    } catch (error) {
        box.innerHTML = `<div class="error">${esc(error.message)}</div>`;
    }
}
