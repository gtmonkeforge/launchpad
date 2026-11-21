import { api, app, auth, date, emptyState, esc, icon, modIcon, money, setTitle, signInUrl, toast } from '/static/js/core.js';
import { appState, call, installDllFiles, installedMods, loaderNames, pageSignal } from '/_launchpad/bridge.js';

// Store listings and purchases are kept between visits so opening the Library again doesn't refetch
// everything. "Check for updates" clears them.
let details = {};
let purchaseCache = null;

const detailKey = mod => mod.guid + '\n' + mod.channel;

// Coming back from the browser may mean something was just bought.
window.addEventListener('launchpad:refresh', () => purchaseCache = null);

export async function render() {
    const signal = pageSignal();
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
            <div class="row"><button class="button subtle" id="install-dll" title="Install a mod DLL from your computer. You can also drop a DLL anywhere in Launchpad.">${icon('upload')}Install DLL</button><input type="file" id="dll-file" accept=".dll" multiple hidden><button class="button subtle" id="refresh">${icon('refresh')}Check for updates</button><button class="button" id="update-all" hidden>${icon('download')}Update all</button></div>
        </div>
        ${state.game.running ? '<div class="notice">Gorilla Tag is running. Close it before installing or removing mods that use files.</div>' : ''}
        <div id="installed"><div class="page-loading"><span class="spinner"></span>Loading…</div></div>
        <div class="section-title"><h2>Purchases</h2><span class="spacer"></span>${auth.user ? `<a class="button ghost small" href="${esc(auth.user.profile_url)}#purchases">${icon('tray')}Receipts & disputes</a>` : ''}</div>
        <div id="purchases"></div>`;

    const draw = async () => {
        const mods = await installedMods();

        if (signal.aborted)
            return;

        const box = app.querySelector('#installed');
        const loader = (await appState()).settings.loader;

        if (!mods.length) {
            box.innerHTML = emptyState('No mods installed yet', 'Find something you like and install it from its page, or drop a mod DLL anywhere in Launchpad.', `<a class="button" href="/browse">${icon('grid')}Browse mods</a>`);
            app.querySelector('#update-all').hidden = true;
            await purchases(mods);
            return;
        }

        // Failed lookups aren't cached, so they're retried on the next visit.
        const fetched = {};
        await Promise.all(mods.filter(m => !m.custom && !details[detailKey(m)]).map(async m => {
            try {
                fetched[m.guid] = details[detailKey(m)] = await api('/api/v1/mods/' + encodeURIComponent(m.guid) + '?channel=' + encodeURIComponent(m.channel));
            } catch {
                fetched[m.guid] = { name: m.name };
            }
        }));

        if (signal.aborted)
            return;

        const detailOf = m => details[detailKey(m)] || fetched[m.guid] || { name: m.name };

        const updatable = mods.filter(m => !m.custom && detailOf(m).current_version && detailOf(m).current_version !== m.version && m.loader === loader);
        app.querySelector('#update-all').hidden = updatable.length < 2;

        box.innerHTML = `<div class="library-list">${mods.map(m => {
            const detail = m.custom ? { name: m.name } : detailOf(m);
            const latest = detail.current_version;
            const inactive = m.loader !== loader;

            return `<article class="surface library-row">
                ${modIcon({ name: m.name, logo: detail.logo })}
                <div>
                    <h3>${m.custom ? esc(m.name) : `<a href="/view/${esc(encodeURIComponent(m.guid))}${m.channel !== 'stable' ? '?channel=' + esc(encodeURIComponent(m.channel)) : ''}">${esc(detail.name || m.name)}</a>`}</h3>
                    <div class="badges">
                        <span class="pill mono">v${esc(m.version)}</span>
                        <span class="pill">${esc(loaderNames[m.loader] || m.loader)}</span>
                        ${m.custom ? '<span class="pill" title="Installed from a DLL on this computer">Custom DLL</span>' : ''}
                        ${m.channel !== 'stable' && !m.custom ? `<span class="pill accent mono">${esc(m.channel)}</span>` : ''}
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

    const dllInput = app.querySelector('#dll-file');
    app.querySelector('#install-dll').onclick = () => dllInput.click();
    dllInput.onchange = async () => {
        const button = app.querySelector('#install-dll');
        const files = [...dllInput.files];
        dllInput.value = '';
        button.disabled = true;

        try {
            await installDllFiles(files, toast);
        } finally {
            button.disabled = false;
        }
    };

    app.querySelector('#refresh').onclick = async event => {
        event.currentTarget.disabled = true;
        details = {};
        purchaseCache = null;
        await draw();
        event.currentTarget.disabled = false;
    };

    app.querySelector('#update-all').onclick = async event => {
        event.currentTarget.disabled = true;
        const loader = (await appState()).settings.loader;

        for (const mod of await installedMods()) {
            const latest = details[detailKey(mod)]?.current_version;

            if (latest && latest !== mod.version && mod.loader === loader && !await update(null, mod))
                break;
        }
    };

    window.addEventListener('launchpad:mods', () => draw().catch(() => {}), { signal });
    await draw();
}

async function purchases(installed) {
    const box = app.querySelector('#purchases');

    if (!auth.user) {
        box.innerHTML = `<div class="surface panel"><p class="hint">Sign in to see the mods you’ve bought and install them on this computer.</p><a class="button small" href="${esc(signInUrl('/library'))}">${icon('user')}Sign in</a></div>`;
        return;
    }

    try {
        if (purchaseCache?.user !== auth.user.id)
            purchaseCache = { user: auth.user.id, items: (await api('/api/v1/purchases')).items };

        const { items } = purchaseCache;

        if (!box.isConnected)
            return;

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
