import { render as siteRender } from './browse.site.js';
import { app } from '../core.js';

let installedOnly = new URLSearchParams(location.search).get('installed') === '1';

// The filter only applies while browse is shown; other pages also list mods.
window.addEventListener('launchpad:navigate', () => installedOnly = false);

const siteFetch = window.fetch.bind(window);
window.fetch = (input, init) => {
    if (installedOnly && typeof input === 'string' && input.startsWith('/api/v1/mods?'))
        input += '&installed=1';

    return siteFetch(input, init);
};

const replaceState = history.replaceState.bind(history);
history.replaceState = (state, title, url) => {
    if (installedOnly && typeof url === 'string' && url.startsWith('/browse'))
        url += (url.includes('?') ? '&' : '?') + 'installed=1';

    replaceState(state, title, url);
};

export async function render() {
    installedOnly = new URLSearchParams(location.search).get('installed') === '1';
    const rendering = siteRender();
    addFilter();
    await rendering;
}

function addFilter() {
    const filters = app.querySelector('#filters');

    if (!filters || filters.querySelector('#installed-filter'))
        return;

    filters.insertAdjacentHTML('afterbegin', `<div class="filter-group"><h2 class="filter-title">Launchpad</h2>
        <label class="check"><input type="checkbox" id="installed-filter" ${installedOnly ? 'checked' : ''}>Installed in Gorilla Tag</label></div>`);

    const rerun = () => app.querySelector('#sort')?.dispatchEvent(new Event('change'));

    filters.querySelector('#installed-filter').onchange = event => {
        installedOnly = event.target.checked;
        rerun();
    };

    filters.querySelector('#reset-filters')?.addEventListener('click', () => {
        if (!installedOnly)
            return;

        installedOnly = false;
        filters.querySelector('#installed-filter').checked = false;
        rerun();
    });
}
