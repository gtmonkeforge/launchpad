import { api, app, applyAccent, auth, boot, readAccent, showError } from './core.js';
import { renderFooter, renderHeader } from './shell.js';

const routes = [
    [/^\/browse$/, () => import('./pages/browse.js')],
    [/^\/view\/([^/]+)\/monkeshield$/, () => import('./pages/shield.js')],
    [/^\/view\/([^/]+)\/(extensions)$/, () => import('./pages/mod.js')],
    [/^\/view\/([^/]+)\/(docs)(?:\/([^/]+))?$/, () => import('./pages/mod.js')],
    [/^\/view\/([^/]+)$/, () => import('./pages/mod.js')],
    [/^\/upload$/, () => import('./pages/publish.js')],
    [/^\/profile\/([^/]+)$/, () => import('./pages/profile.js')],
    [/^\/settings(?:\/([a-z]+))?(?:\/([a-f0-9]{24}))?$/, () => import('./pages/settings.js')],
    [/^\/(login|signup|verify-email|reset-password|unsubscribe)$/, () => import('./pages/auth.js')],
    [/^\/(friends|chat|groups|transfers|notifications)$/, () => import('./pages/social.js')],
    [/^\/software(?:\/([a-z]+))?$/, () => import('./pages/software.js')],
    [/^\/(terms|privacy)$/, () => import('./pages/legal.js')],
    [/^\/docs(?:\/(.+))?$/, () => import('./pages/docs.js')],
    [/^\/library$/, () => import('/_launchpad/pages/library.js')],
    [/^\/launchpad(?:\/([a-z]+))?$/, () => import('/_launchpad/pages/app-settings.js')],
];

// Pages that can be swapped in place without a full reload. The rest (publishing, docs editing, chat, sign-in)
// keep page-wide state or unsaved-work guards, so moving to or from them still reloads.
// `same` marks kinds where going between two pages of that kind is safe too; for the others, late requests
// from the old page could land in the new page's identical elements.
const softKinds = [
    { kind: 'browse', pattern: /^\/browse$/ },
    { kind: 'mod', pattern: /^\/view\/[^/]+(?:\/extensions|\/docs(?:\/[^/]+)?)?$/ },
    { kind: 'profile', pattern: /^\/profile\/[^/]+$/ },
    { kind: 'settings', pattern: /^\/(?:settings|launchpad)(?:\/[a-z]+)?(?:\/[a-f0-9]{24})?$/, same: true },
    { kind: 'library', pattern: /^\/library$/, same: true },
    { kind: 'legal', pattern: /^\/(?:terms|privacy)$/, same: true },
    { kind: 'software', pattern: /^\/software(?:\/[a-z]+)?$/, same: true },
];

const kindOf = path => softKinds.find(entry => entry.pattern.test(path));

let busy = false;
let shownPath = location.pathname;
let shownKey = location.pathname + location.search;

function canSwap(from, to) {
    const a = kindOf(from), b = kindOf(to);
    return !busy && !!a && !!b && (a !== b || !!a.same);
}

function redirectFor(path) {
    if (path === '/profile')
        return auth.user ? auth.user.profile_url : '/login?next=%2Fprofile';

    // Purchases live on your profile; go there directly instead of loading settings first.
    if (path === '/settings/purchases')
        return auth.user ? auth.user.profile_url + '#purchases' : '/login?next=%2Fsettings';

    return null;
}

const replaceState = history.replaceState.bind(history);
history.replaceState = (...args) => {
    replaceState(...args);
    shownPath = location.pathname;
    shownKey = location.pathname + location.search;
};

async function renderRoute() {
    for (let hops = 0; hops < 3; hops++) {
        const target = redirectFor(location.pathname);

        if (!target)
            break;

        history.replaceState(null, '', target);
    }

    const path = decodeURIComponent(location.pathname);

    for (const [pattern, load] of routes) {
        const match = path.match(pattern);

        if (!match)
            continue;

        try {
            const page = await load();
            await page.render(...match.slice(1));
        } catch (error) {
            showError('Unable to load this page', error.message);
        }

        return;
    }

    showError('Page not found', 'The requested page does not exist.');
}

function syncHeader() {
    const path = location.pathname;

    document.querySelectorAll('#main-nav a').forEach(link => {
        const href = link.dataset.lpHref ?? link.getAttribute('href');
        const active = href === '/browse' ? path === '/browse' || path.startsWith('/view/') : path === href || path.startsWith(href + '/');

        if (active)
            link.setAttribute('aria-current', 'page');
        else
            link.removeAttribute('aria-current');
    });

    const search = document.querySelector('#header-q');

    if (search && document.activeElement !== search)
        search.value = path === '/browse' ? new URLSearchParams(location.search).get('q') || '' : '';

    document.querySelectorAll('.header-actions .dropdown').forEach(menu => menu.remove());
    document.querySelectorAll('.header-actions [aria-expanded="true"]').forEach(button => button.setAttribute('aria-expanded', 'false'));
}

async function show() {
    busy = true;
    shownPath = location.pathname;
    shownKey = location.pathname + location.search;
    window.dispatchEvent(new Event('launchpad:navigate'));
    syncHeader();
    app.innerHTML = '<div class="page-loading" role="status"><span class="spinner" aria-hidden="true"></span>Loading…</div>';
    document.body.scrollTo(0, 0);

    try {
        await renderRoute();
    } finally {
        busy = false;
    }

    if (location.hash.length > 1 && location.hash !== '#purchases')
        document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView({ block: 'start' });
}

// Called by launchpad.js for every in-app link. Returns false when the page has to load normally.
window.__lpRoute = (url, replace = false) => {
    const key = url.pathname + url.search;

    if (key === shownKey && url.hash && !busy) {
        location.hash = url.hash;
        return true;
    }

    if (!canSwap(shownPath, url.pathname))
        return false;

    history[replace ? 'replaceState' : 'pushState'](null, '', key + url.hash);
    show();
    return true;
};

// Re-renders the current page after something may have changed elsewhere, without reloading the app.
window.__lpRefresh = () => {
    if (busy || !kindOf(location.pathname))
        return false;

    const before = auth.user?.id || null;

    window.dispatchEvent(new Event('launchpad:refresh'));

    api('/api/v1/session').then(session => {
        Object.assign(auth, session);

        if ((auth.user?.id || null) !== before)
            location.reload();
        else
            show();
    }).catch(() => show());

    return true;
};

window.addEventListener('popstate', () => {
    if (location.pathname + location.search === shownKey)
        return;

    if (canSwap(shownPath, location.pathname))
        show();
    else
        location.reload();
});

document.addEventListener('submit', event => {
    const form = event.target;

    if (form.id !== 'header-search' || event.defaultPrevented)
        return;

    event.preventDefault();
    const q = new FormData(form).get('q')?.toString().trim() || '';
    window.__lpNavigate('/browse' + (q ? '?q=' + encodeURIComponent(q) : ''));
});

async function start() {
    applyAccent(readAccent());

    try {
        Object.assign(auth, await api('/api/v1/session'));
    } catch {
    }

    applyAccent(auth.user?.accent_color || readAccent());
    renderHeader();
    renderFooter();

    if (boot.error) {
        showError(boot.status === 404 ? 'Page not found' : 'Something went wrong', boot.error);
        return;
    }

    busy = true;

    try {
        await renderRoute();
    } finally {
        busy = false;
    }
}

start();
