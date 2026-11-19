import { api, app, auth, brand, capLabels, capTips, categories, categoryIcons, emptyState, esc, icon, loaderLabels, modCard, pagination, setTitle, skeletonGrid } from '../core.js';

export async function render() {
    const params = new URLSearchParams(location.search);
    const state = {
        q: params.get('q') || '',
        category: params.get('category') || '',
        capability: params.get('capability') || '',
        loader: params.get('loader') || '',
        type: params.get('type') || '',
        price: params.get('price') || '',
        sort: params.get('sort') || 'newest',
        mine: params.get('mine') === 'true',
        page: Number(params.get('page')) || 1,
    };
    app.innerHTML = `
        <div class="page-head"><h1>${state.mine ? 'Your mods' : 'Mods'}</h1><a class="button" href="/upload">${icon('plus')}Publish a mod</a></div>
        <div class="browse-layout">
            <aside class="filters surface" id="filters" aria-label="Filters">
                <div class="filter-group"><h2 class="filter-title">Category</h2><div class="category-list">
                    <button class="category" data-category="">${icon('grid')}All mods</button>
                    ${categories.map(c => `<button class="category" data-category="${c}">${icon(categoryIcons[c])}${c}</button>`).join('')}
                </div></div>
                <div class="filter-group"><h2 class="filter-title">Capabilities</h2>
                    ${Object.entries(capLabels).map(([key, label]) => `<label class="check" title="${esc(capTips[key])}"><input type="radio" name="capability" value="${key}">${label}</label>`).join('')}
                    <label class="check"><input type="radio" name="capability" value="">Any</label>
                </div>
                <div class="filter-group"><h2 class="filter-title">Price</h2>
                    <label class="check"><input type="radio" name="price" value="">Any price</label>
                    <label class="check"><input type="radio" name="price" value="free">Free</label>
                    <label class="check"><input type="radio" name="price" value="paid">Paid</label>
                </div>
                <div class="filter-group"><label class="filter-title" for="loader-filter">Mod loader</label><select id="loader-filter"><option value="">All loaders</option>${Object.entries(loaderLabels).map(([v, l]) => `<option value="${v}">${l}</option>`).join('')}</select></div>
                <div class="filter-group"><label class="filter-title" for="type-filter">Format</label><select id="type-filter"><option value="">Any format</option><option value="dll">DLL</option><option value="zip">ZIP</option></select></div>
                ${auth.user ? '<div class="filter-group"><label class="check"><input type="checkbox" id="mine-filter">Only my mods</label></div>' : ''}
                <div class="filter-group"><button class="button ghost small block" id="reset-filters">Reset filters</button></div>
            </aside>
            <section aria-label="Results">
                <div class="toolbar">
                    <button class="button subtle small filters-toggle" id="filters-toggle" aria-controls="filters" aria-expanded="false">${icon('filter')}Filters</button>
                    <form class="toolbar-search" id="page-search" role="search">${icon('search')}<label class="sr-only" for="page-q">Search mods</label><input id="page-q" type="search" placeholder="Search by name, author or GUID" maxlength="200" value="${esc(state.q)}"></form>
                    <span class="count" id="result-count" aria-live="polite"></span>
                    <div class="active-filters" id="active-filters"></div>
                    <span class="spacer"></span>
                    <label class="sr-only" for="sort">Sort</label>
                    <select id="sort"><option value="newest">Recently updated</option><option value="popular">Most downloaded</option><option value="name">Name A–Z</option></select>
                </div>
                <div id="results">${skeletonGrid()}</div>
                <div id="pages"></div>
            </section>
        </div>`;

    const results = app.querySelector('#results');
    const sort = app.querySelector('#sort');
    const loader = app.querySelector('#loader-filter');
    const type = app.querySelector('#type-filter');
    const mine = app.querySelector('#mine-filter');
    let request = 0;

    sort.value = state.sort;
    loader.value = state.loader;
    type.value = state.type;

    if (mine)
        mine.checked = state.mine;

    app.querySelector(`[name=capability][value="${state.capability}"]`).checked = true;
    (app.querySelector(`[name=price][value="${state.price}"]`) || app.querySelector('[name=price][value=""]')).checked = true;

    // Matches the server's titles so search engines see the same one before and after the script runs.
    function title() {
        if (state.mine)
            return 'Your mods';

        const mods = brand.game ? `${brand.game} mods` : 'Mods';
        return state.category ? `${state.category} ${brand.game ? mods : 'mods'}` : mods;
    }

    function sync() {
        setTitle(title());
        app.querySelectorAll('[data-category]').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.category === state.category)));
        const chips = [];

        if (state.q)
            chips.push(['q', `“${state.q}”`]);

        if (state.category)
            chips.push(['category', state.category]);

        if (state.capability)
            chips.push(['capability', capLabels[state.capability]]);

        if (state.loader)
            chips.push(['loader', loaderLabels[state.loader]]);

        if (state.type)
            chips.push(['type', state.type.toUpperCase()]);

        if (state.price)
            chips.push(['price', state.price === 'free' ? 'Free' : 'Paid']);

        app.querySelector('#active-filters').innerHTML = chips.map(([key, label]) => `<button class="pill accent" data-clear="${key}">${esc(label)} ${icon('x')}</button>`).join('');
        app.querySelectorAll('[data-clear]').forEach(b => b.onclick = () => {
            state[b.dataset.clear] = '';

            if (b.dataset.clear === 'price')
                app.querySelector('[name=price][value=""]').checked = true;

            if (b.dataset.clear === 'capability')
                app.querySelector('[name=capability][value=""]').checked = true;

            if (b.dataset.clear === 'q') {
                document.querySelector('#header-q').value = '';
                app.querySelector('#page-q').value = '';
            }

            reload();
        });
    }

    async function load() {
        const id = ++request;
        sync();
        const query = new URLSearchParams({ sort: state.sort, page: String(state.page), limit: '12' });

        for (const key of ['q', 'category', 'capability', 'loader', 'type', 'price'])
            if (state[key])
                query.set(key, state[key]);

        if (state.mine)
            query.set('mine', 'true');

        history.replaceState(null, '', '/browse?' + query);

        try {
            const data = await api('/api/v1/mods?' + query);

            if (id !== request)
                return;

            app.querySelector('#result-count').innerHTML = `<strong>${data.total}</strong> ${data.total === 1 ? 'mod' : 'mods'}`;
            const filtered = state.q || state.category || state.capability || state.loader || state.type || state.price || state.mine;
            results.innerHTML = data.items.length
                ? `<div class="mod-grid">${data.items.map(modCard).join('')}</div>`
                : emptyState(filtered ? 'No mods match' : 'No mods yet', filtered ? 'Try a different search or clear a filter.' : 'No mods have been published yet.', '<a class="button subtle" href="/upload">Publish a mod</a>');
            pagination(app.querySelector('#pages'), data.total, data.limit, state.page, page => {
                state.page = page;
                load();
                results.scrollIntoView({ behavior: 'smooth', block: 'start' });
            });
        } catch (error) {
            if (id === request)
                results.innerHTML = `<div class="error">${esc(error.message)}</div>`;
        }
    }

    function reload() {
        state.page = 1;
        load();
    }

    app.querySelectorAll('[data-category]').forEach(b => b.onclick = () => {
        state.category = b.dataset.category;
        reload();
    });
    app.querySelectorAll('[name=capability]').forEach(r => r.onchange = () => {
        state.capability = r.value;
        reload();
    });
    app.querySelectorAll('[name=price]').forEach(r => r.onchange = () => {
        state.price = r.value;
        reload();
    });
    sort.onchange = () => { state.sort = sort.value; reload(); };
    loader.onchange = () => { state.loader = loader.value; reload(); };
    type.onchange = () => { state.type = type.value; reload(); };

    if (mine)
        mine.onchange = () => { state.mine = mine.checked; reload(); };

    app.querySelector('#reset-filters').onclick = () => {
        Object.assign(state, { q: '', category: '', capability: '', loader: '', type: '', price: '', sort: 'newest', mine: false });
        sort.value = 'newest';
        loader.value = '';
        type.value = '';
        app.querySelector('[name=capability][value=""]').checked = true;
        app.querySelector('[name=price][value=""]').checked = true;

        if (mine)
            mine.checked = false;

        reload();
    };

    const toggle = app.querySelector('#filters-toggle');
    toggle.onclick = () => {
        const filters = app.querySelector('#filters');
        filters.classList.toggle('open');
        toggle.setAttribute('aria-expanded', String(filters.classList.contains('open')));
    };

    const headerSearch = document.querySelector('#header-search');
    let timer;
    headerSearch.onsubmit = event => {
        event.preventDefault();
        state.q = headerSearch.q.value.trim();
        app.querySelector('#page-q').value = state.q;
        reload();
    };
    headerSearch.q.oninput = () => {
        clearTimeout(timer);
        timer = setTimeout(() => {
            state.q = headerSearch.q.value.trim();
            app.querySelector('#page-q').value = state.q;
            reload();
        }, 250);
    };

    const pageSearch = app.querySelector('#page-search');
    pageSearch.onsubmit = event => {
        event.preventDefault();
        state.q = pageSearch.querySelector('input').value.trim();
        headerSearch.q.value = state.q;
        reload();
    };
    pageSearch.querySelector('input').oninput = event => {
        clearTimeout(timer);
        timer = setTimeout(() => {
            state.q = event.target.value.trim();
            headerSearch.q.value = state.q;
            reload();
        }, 250);
    };

    await load();
}
