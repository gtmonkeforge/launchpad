import { render as siteRender } from './mod.site.js';
import { api, app, esc, icon, toast } from '../core.js';
import { appState, call, installedMods, loaderNames, pageSignal } from '/_launchpad/bridge.js';

export { shieldRing, shieldState } from './mod.site.js';

// section and docSlug come from /view/<id>/extensions and /view/<id>/docs/<slug>.
export async function render(id, section, docSlug) {
    const signal = pageSignal();
    await siteRender(id, section, docSlug);

    if (signal.aborted)
        return;

    await enhance(id);
    window.addEventListener('launchpad:mods', () => enhance(id).catch(() => {}), { signal });
}

async function enhance(id) {
    const channel = new URLSearchParams(location.search).get('channel') || 'stable';
    const [mod, state, installed] = await Promise.all([
        api('/api/v1/mods/' + encodeURIComponent(id) + '?channel=' + encodeURIComponent(channel)),
        appState(),
        installedMods(),
    ]);
    const current = mod.releases.find(r => r.version === mod.current_version) || mod.releases[0];

    if (!current || (mod.gated && !mod.owned))
        return;

    const record = installed.find(m => m.guid === mod.guid);
    const html = controls(mod, current, state, record);
    const hero = app.querySelector('.hero-actions');
    const first = hero?.firstElementChild;

    if (first && !first.classList.contains('row'))
        first.outerHTML = `<div class="lp-install">${html.hero}</div>`;

    const card = app.querySelector('#download');

    if (card) {
        card.querySelectorAll('.loader-group, .store-card, .lp-install').forEach(el => el.remove());
        card.querySelectorAll('details').forEach(el => {
            if (el.querySelector('summary')?.textContent.startsWith('SHA-256'))
                el.remove();
        });
        card.querySelector('.version')?.insertAdjacentHTML('afterend', `<div class="lp-install">${html.side}</div>`);
    }

    app.querySelectorAll('.lp-install [data-lp-action]').forEach(button => button.onclick = () => act(button, mod));
}

function controls(mod, current, state, record) {
    const loader = state.settings.loader;
    const loaders = current.loaders?.length ? current.loaders : ['bepinex5'];
    const note = (glyph, text) => `<p class="lp-note">${icon(glyph)} ${esc(text)}</p>`;
    let primary = '';
    let notes = '';

    if (!state.game.valid) {
        primary = `<a class="button large" href="/launchpad/game">${icon('game')}Choose your game folder</a>`;
        notes = note('game', 'Launchpad needs to know where Gorilla Tag is installed.');
    } else if (record) {
        if (record.loader !== loader) {
            notes = note('swap', `Installed for ${loaderNames[record.loader]}. Switch back in Launchpad settings to use it.`);
        } else if (record.version !== current.version) {
            primary = `<button class="button large" data-lp-action="install">${icon('refresh')}Update to v${esc(current.version)}</button>`;
        } else {
            primary = `<button class="button large subtle" disabled>${icon('check')}Installed · v${esc(record.version)}</button>`;
        }

        primary += `<button class="button danger" data-lp-action="remove">${icon('trash')}Remove</button>`;
    } else if (!loaders.includes(loader)) {
        primary = `<button class="button large" disabled>${icon('download')}Install</button>`;
        notes = note('swap', `Not available for ${loaderNames[loader]}. It supports ${loaders.map(l => loaderNames[l] || l).join(', ')}.`);
    } else {
        primary = `<button class="button large" data-lp-action="install">${icon('download')}Install${mod.uses_injector ? ' with injector' : ''}</button>`;
    }

    if (mod.uses_injector && state.game.valid)
        notes += note('shield', 'Delivered encrypted and loaded into the game from memory each time Gorilla Tag starts while Launchpad is open.');

    return { hero: primary, side: primary + notes };
}

async function act(button, mod) {
    const channel = new URLSearchParams(location.search).get('channel') || 'stable';
    button.disabled = true;

    try {
        if (button.dataset.lpAction === 'remove') {
            if (!confirm(`Remove ${mod.name} from Gorilla Tag?`)) {
                button.disabled = false;
                return;
            }

            await call('mods.uninstall', { guid: mod.guid });
            toast(`${mod.name} was removed.`);
        } else {
            const result = await call('mods.install', { guid: mod.guid, channel });
            toast(result.injector
                ? `${mod.name} is ready. It loads the next time you start Gorilla Tag with Launchpad open.`
                : `${mod.name} v${result.version} is installed.`);
        }
    } catch (error) {
        toast(error.message, 'error');
        button.disabled = false;
    }
}
