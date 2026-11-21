import { api, auth, esc, icon, modCard, pagination, signInUrl, toast } from '../core.js';

const stateLabels = { approved: 'Approved', pending: 'Pending', unapproved: 'Unapproved' };

// The Extensions tab: every visitor sees the approved list; the mod's developers can also review pending and
// unapproved extensions, approve or unapprove them, and feature the ones they want shown first.
export async function extensionsPanel(root, mod) {
    const base = '/api/v1/mods/' + encodeURIComponent(mod.guid) + '/extensions';
    const view = { state: 'approved', q: '', page: 1 };
    const publishHref = '/upload?extends=' + encodeURIComponent(mod.guid);

    root.innerHTML = `
        <div class="ext-head">
            <div><h2>Extensions for ${esc(mod.name)}</h2><p class="hint">Free add-ons made by the community. Each one is reviewed by staff, then listed here once ${esc(mod.name)}’s developers approve it.</p></div>
            <a class="button" href="${auth.user ? publishHref : signInUrl()}">${icon('plus')}Publish an extension</a>
        </div>
        <div class="ext-toolbar">
            <div class="docs-search grow">${icon('search')}<label class="sr-only" for="ext-q">Search extensions</label><input id="ext-q" type="search" placeholder="Search extensions" maxlength="200" autocomplete="off"></div>
            <div class="segmented" id="ext-states" role="tablist" aria-label="Moderation state" hidden></div>
        </div>
        <div id="ext-list"><p class="hint">Loading…</p></div>
        <div id="ext-pages"></div>
        <details class="surface panel ext-api">
            <summary>${icon('code')} Build a marketplace with the Extensions API</summary>
            <p class="hint">Your mod can list and install the same extensions in-game. The list is public, needs no key, and only ever includes extensions you’ve approved. Each item includes <code>latest_release.downloads</code> with direct download URLs.</p>
            <pre><code>GET ${esc(location.origin)}/api/v1/mods/${esc(encodeURIComponent(mod.guid))}/extensions?sort=featured&amp;page=1&amp;limit=24</code></pre>
            <p class="hint">Filter with <code>q</code>, <code>category</code> or <code>featured=true</code>, and sort by <code>featured</code>, <code>popular</code>, <code>newest</code> or <code>name</code>. <a href="/docs/guides/extensions">Read the guide</a>.</p>
        </details>`;

    const list = root.querySelector('#ext-list');
    const states = root.querySelector('#ext-states');
    let timer;

    root.querySelector('#ext-q').addEventListener('input', event => {
        clearTimeout(timer);
        timer = setTimeout(() => {
            view.q = event.target.value.trim();
            view.page = 1;
            load();
        }, 250);
    });

    async function load() {
        const params = new URLSearchParams({ state: view.state, page: String(view.page), limit: '24' });

        if (view.q)
            params.set('q', view.q);

        let result;

        try {
            result = await api(base + '?' + params);
        } catch (error) {
            list.innerHTML = `<p class="hint">${esc(error.message)}</p>`;
            return;
        }

        if (result.can_moderate) {
            states.hidden = false;
            states.innerHTML = Object.entries(stateLabels).map(([key, label]) =>
                `<button type="button" role="tab" data-state="${key}" aria-selected="${key === view.state}">${label}<span class="count">${result.counts[key]}</span></button>`).join('');
            states.querySelectorAll('[data-state]').forEach(button => button.onclick = () => {
                view.state = button.dataset.state;
                view.page = 1;
                load();
            });
        }

        list.innerHTML = result.items.length
            ? `<div class="mod-grid">${result.items.map(item => card(item, result.can_moderate)).join('')}</div>`
            : `<div class="empty"><h2>${emptyTitle(view)}</h2><p>${emptyText(view, mod)}</p></div>`;

        list.querySelectorAll('[data-moderate]').forEach(button => button.onclick = () => moderate(button));
        pagination(root.querySelector('#ext-pages'), result.total, result.limit, result.page, page => {
            view.page = page;
            load();
        });

        const tabCount = document.querySelector('#extension-count');

        if (tabCount && view.state === 'approved' && !view.q)
            tabCount.textContent = result.total ? String(result.total) : '';
    }

    async function moderate(button) {
        const change = JSON.parse(button.dataset.moderate);
        button.disabled = true;

        try {
            await api(base + '/' + encodeURIComponent(button.dataset.guid), { method: 'PATCH', json: change });
            toast(change.featured === true ? 'Extension featured.' : change.featured === false ? 'No longer featured.' : change.state === 'approved' ? 'Extension approved.' : 'Extension unapproved.');
            await load();
        } catch (error) {
            toast(error.message, 'error');
            button.disabled = false;
        }
    }

    await load();
}

function card(item, canModerate) {
    const featured = item.extends?.featured;
    const state = item.extends?.state;
    const action = (label, iconName, change, kind = 'subtle') =>
        `<button type="button" class="button ${kind} small" data-guid="${esc(item.guid)}" data-moderate='${esc(JSON.stringify(change))}'>${icon(iconName)}${label}</button>`;

    let actions = featured ? `<span class="pill accent">${icon('star')}Featured</span>` : '';

    if (canModerate) {
        if (state === 'approved')
            actions += action(featured ? 'Unfeature' : 'Feature', 'star', { featured: !featured }) + action('Unapprove', 'x', { state: 'unapproved' }, 'ghost');
        else
            actions += action('Approve', 'check', { state: 'approved' }, '') + (state === 'pending' ? action('Unapprove', 'x', { state: 'unapproved' }, 'ghost') : '');
    }

    return `<div class="ext-item${featured ? ' featured' : ''}">${modCard(item)}${actions ? `<div class="ext-actions">${actions}</div>` : ''}</div>`;
}

function emptyTitle(view) {
    if (view.q)
        return 'No matching extensions';

    return { approved: 'No extensions yet', pending: 'Nothing waiting for review', unapproved: 'No unapproved extensions' }[view.state];
}

function emptyText(view, mod) {
    if (view.q)
        return 'Try a different search.';

    return {
        approved: `Extensions for ${mod.name} will appear here once they’re approved.`,
        pending: 'New extensions show up here after staff review them.',
        unapproved: 'Extensions you unapprove are hidden from this tab and the Extensions API.',
    }[view.state];
}
