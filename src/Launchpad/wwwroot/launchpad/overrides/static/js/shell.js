import { renderFooter as siteFooter, renderHeader as siteHeader } from './shell.site.js';
import { auth, esc, icon } from './core.js';

export function renderHeader() {
    siteHeader();

    const nav = document.querySelector('#main-nav');
    const path = location.pathname;
    nav?.querySelector('a[href="/software"]')?.remove();
    nav?.querySelector('a[href="/browse"]')?.insertAdjacentHTML('afterend', `<a href="/library" ${path === '/library' ? 'aria-current="page"' : ''}>Library</a>`);

    const navSlot = document.querySelector('#lp-site-nav');
    const toolsSlot = document.querySelector('#lp-site-tools');
    const inner = document.querySelector('#site-header .header-inner');

    if (navSlot && toolsSlot && inner) {
        navSlot.replaceChildren(...[nav].filter(Boolean));
        toolsSlot.replaceChildren(...['#header-search', '.header-actions'].map(selector => inner.querySelector(selector)).filter(Boolean));
    }

    const actions = document.querySelector('.header-actions');

    if (!actions)
        return;

    new MutationObserver(() => {
        const menu = actions.querySelector('.dropdown[role="menu"]:not([data-lp])');

        if (!menu)
            return;

        menu.dataset.lp = '';
        const entries = `
            <div class="label">Launchpad</div>
            <a role="menuitem" href="/library">${icon('tray')}Library</a>
            <a role="menuitem" href="/launchpad">${icon('game')}Game & mod loader</a>`;

        const settings = menu.querySelector('a[href="/settings"]');

        if (auth.user && settings)
            settings.previousElementSibling?.insertAdjacentHTML('beforebegin', `${entries}<hr>`);
        else
            menu.insertAdjacentHTML('afterbegin', entries + '<hr>');
    }).observe(actions, { childList: true, subtree: true });
}

export function renderFooter() {
    siteFooter();

    const version = document.querySelector('#launchpad-version');

    if (!version) {
        const boot = JSON.parse(document.querySelector('#boot')?.textContent || '{}');
        document.querySelector('.footer-inner .quip')?.insertAdjacentHTML('afterend', `<a class="version-label" id="launchpad-version" href="/launchpad/about">Launchpad v${esc(boot.launchpad?.version || '')}</a>`);
    }
}
