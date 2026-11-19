import { api, app, date, esc, icon, setTitle, toast } from '../core.js';

const INDEX = 'index';

export async function render(path = '') {
    const slug = path.replace(/^\/+|\/+$/g, '') || INDEX;
    const [index, page] = await Promise.all([
        api('/api/v1/docs'),
        api('/api/v1/docs/pages/' + slug).catch(() => null),
    ]);

    const editing = index.can_edit && (new URLSearchParams(location.search).has('edit') || (!page && slug !== INDEX));

    app.innerHTML = `<div class="docs-layout">
        <aside class="docs-sidebar" aria-label="Documentation">
            <div class="docs-search">
                ${icon('search')}
                <label class="sr-only" for="docs-q">Search documentation</label>
                <input id="docs-q" type="search" placeholder="Search docs" autocomplete="off" maxlength="200">
            </div>
            <button class="button subtle small docs-nav-toggle" id="docs-nav-toggle" aria-expanded="false" aria-controls="docs-nav">${icon('menu')}Browse docs</button>
            <nav class="docs-nav" id="docs-nav"></nav>
            ${index.can_edit ? `<form class="docs-new" id="docs-new">
                <label class="sr-only" for="docs-new-slug">New page address</label>
                <input id="docs-new-slug" placeholder="section/new-page" spellcheck="false" autocomplete="off" maxlength="200">
                <button class="button subtle small" type="submit">${icon('plus')}New page</button>
            </form>` : ''}
        </aside>
        <div class="docs-main" id="docs-main"></div>
    </div>`;

    const nav = app.querySelector('#docs-nav');
    const search = app.querySelector('#docs-q');
    const pages = index.pages;

    const drawNav = () => {
        const query = search.value.trim();
        nav.innerHTML = query ? results(pages, query) : tree(pages, slug);
    };

    search.addEventListener('input', drawNav);
    search.addEventListener('keydown', event => {
        if (event.key === 'Enter') {
            const first = nav.querySelector('a');

            if (first)
                location.href = first.href;
        } else if (event.key === 'Escape' && search.value) {
            search.value = '';
            drawNav();
        }
    });

    const toggle = app.querySelector('#docs-nav-toggle');
    toggle.onclick = () => {
        nav.classList.toggle('open');
        toggle.setAttribute('aria-expanded', String(nav.classList.contains('open')));
    };

    // On a phone the list is hidden, so typing a search opens it.
    search.addEventListener('input', () => {
        nav.classList.toggle('open', Boolean(search.value.trim()) || toggle.getAttribute('aria-expanded') === 'true');
    });

    app.querySelector('#docs-new')?.addEventListener('submit', event => {
        event.preventDefault();
        const value = app.querySelector('#docs-new-slug').value.trim().toLowerCase().replace(/\.md$/, '').replace(/^\/+|\/+$/g, '');

        if (!/^[a-z0-9][a-z0-9-]*(\/[a-z0-9][a-z0-9-]*){0,3}$/.test(value)) {
            toast('Use lowercase letters, numbers and dashes, with up to three folders, like creators/payouts.', 'error');
            return;
        }

        location.href = (value === INDEX ? '/docs' : '/docs/' + value) + '?edit';
    });

    drawNav();
    const main = app.querySelector('#docs-main');

    if (editing)
        editor(main, slug, page);
    else if (page)
        showPage(main, page, pages, index.can_edit);
    else
        landing(main, pages, index.can_edit);
}

function grouped(pages) {
    const groups = new Map();

    for (const page of pages) {
        if (page.slug === INDEX)
            continue;

        if (!groups.has(page.section))
            groups.set(page.section, []);

        groups.get(page.section).push(page);
    }

    // Pages outside any folder come first, before the named sections.
    return [...groups.entries()].sort(([a], [b]) => (a !== '') - (b !== ''));
}

function link(page, current) {
    return `<a href="${esc(page.url)}" ${page.slug === current ? 'aria-current="page"' : ''}>${esc(page.title)}</a>`;
}

function tree(pages, current) {
    const home = pages.find(page => page.slug === INDEX);
    let html = home ? link({ ...home, title: home.title || 'Overview' }, current) : `<a href="/docs" ${current === INDEX ? 'aria-current="page"' : ''}>Overview</a>`;

    for (const [section, items] of grouped(pages)) {
        if (section)
            html += `<h2>${esc(section)}</h2>`;

        html += items.map(page => link(page, current)).join('');
    }

    return html;
}

function terms(query) {
    return [...new Set(query.toLowerCase().split(/\s+/).filter(Boolean))];
}

// Escapes the text and wraps each matched term in <mark>.
function highlight(text, words) {
    if (!words.length)
        return esc(text);

    const pattern = new RegExp(`(${words.map(word => word.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|')})`, 'gi');
    return text.split(pattern).map((part, i) => i % 2 ? `<mark>${esc(part)}</mark>` : esc(part)).join('');
}

function snippet(text, words) {
    const lower = text.toLowerCase();
    const at = Math.min(...words.map(word => lower.indexOf(word)).filter(i => i >= 0));

    if (!Number.isFinite(at))
        return '';

    const start = Math.max(0, at - 50);
    const end = Math.min(text.length, at + 90);
    return (start > 0 ? '…' : '') + text.slice(start, end).trim() + (end < text.length ? '…' : '');
}

function results(pages, query) {
    const words = terms(query);
    const matches = pages
        .map(page => {
            const title = page.title.toLowerCase();
            const haystack = `${title} ${page.section.toLowerCase()} ${page.text.toLowerCase()}`;

            if (!words.every(word => haystack.includes(word)))
                return null;

            const score = words.reduce((sum, word) => sum + (title.includes(word) ? 10 : 0) + (title.startsWith(word) ? 5 : 0), 0);
            return { page, score };
        })
        .filter(Boolean)
        .sort((a, b) => b.score - a.score);

    if (!matches.length)
        return `<p class="hint docs-empty">No pages match “${esc(query)}”.</p>`;

    return `<p class="sr-only" role="status">${matches.length} ${matches.length === 1 ? 'page' : 'pages'} found</p>` + matches.map(({ page }) => {
        const found = snippet(page.text, words);
        return `<a class="docs-result" href="${esc(page.url)}">
            <strong>${highlight(page.title, words)}</strong>
            ${page.section ? `<small>${esc(page.section)}</small>` : ''}
            ${found ? `<span>${highlight(found, words)}</span>` : ''}
        </a>`;
    }).join('');
}

function showPage(main, page, pages, canEdit) {
    setTitle(page.slug === INDEX ? 'Docs' : page.title + ' · Docs');

    const order = [pages.find(p => p.slug === INDEX), ...grouped(pages).flatMap(([, items]) => items)].filter(Boolean);
    const at = order.findIndex(p => p.slug === page.slug);
    const prev = order[at - 1];
    const next = order[at + 1];

    main.innerHTML = `<article>
        <header class="page-head docs-head">
            <div>
                ${page.section ? `<p class="docs-kicker">${esc(page.section)}</p>` : ''}
                <h1>${esc(page.title)}</h1>
                <p class="hint">Updated ${esc(date(page.updated_at))}</p>
            </div>
            ${canEdit ? `<a class="button subtle small" href="${esc(page.url)}?edit">${icon('edit')}Edit</a>` : ''}
        </header>
        <div class="markdown docs-body">${page.html}</div>
        ${prev || next ? `<nav class="docs-pager" aria-label="More pages">
            ${prev ? `<a class="surface" href="${esc(prev.url)}"><small>Previous</small>${icon('back')}${esc(prev.title)}</a>` : '<span></span>'}
            ${next ? `<a class="surface next" href="${esc(next.url)}"><small>Next</small>${esc(next.title)}${icon('next')}</a>` : ''}
        </nav>` : ''}
    </article>`;

    // The server prefixes heading ids so they can't clash with the site's own, so plain #section links are mapped to them.
    if (location.hash && !location.hash.startsWith('#doc-')) {
        const target = document.getElementById('doc-' + decodeURIComponent(location.hash.slice(1)));
        target?.scrollIntoView();
    }
}

function landing(main, pages, canEdit) {
    setTitle('Docs');
    const groups = grouped(pages);

    main.innerHTML = `<header class="page-head docs-head"><div><h1>Documentation</h1><p>Guides and reference.</p></div>
        ${canEdit ? `<a class="button subtle small" href="/docs?edit">${icon('edit')}Write a home page</a>` : ''}</header>
        ${groups.length ? `<div class="docs-cards">${groups.map(([section, items]) => `<section class="surface panel">
            <h2>${esc(section || 'General')}</h2>
            <ul>${items.map(page => `<li><a href="${esc(page.url)}">${esc(page.title)}</a>${page.description ? `<p class="hint">${esc(page.description)}</p>` : ''}</li>`).join('')}</ul>
        </section>`).join('')}</div>` : `<p class="hint">No pages have been written yet.${canEdit ? ' Use “New page” in the sidebar to start one.' : ''}</p>`}`;
}

function editor(main, slug, page) {
    const isNew = !page;
    const url = slug === INDEX ? '/docs' : '/docs/' + slug;
    const leaf = slug.split('/').pop().replace(/-/g, ' ');
    const starter = `# ${leaf.charAt(0).toUpperCase() + leaf.slice(1)}\n\n`;
    let saved = isNew ? '' : page.markdown;
    let version = page?.version ?? null;

    setTitle((isNew ? 'New page' : 'Editing ' + page.title) + ' · Docs');
    main.innerHTML = `<form class="docs-editor" id="docs-editor">
        <header class="page-head docs-head">
            <div>
                <h1>${isNew ? 'New page' : 'Edit page'}</h1>
                <p class="hint mono">${esc(url)} · docs/${esc(slug)}.md</p>
            </div>
            <div class="row">
                ${isNew ? '' : `<button class="button danger small" type="button" id="docs-delete">${icon('trash')}Delete</button>`}
                <a class="button ghost small" href="${isNew ? '/docs' : esc(url)}">Cancel</a>
                <button class="button small" type="submit" id="docs-save">${icon('check')}${isNew ? 'Create page' : 'Save'}</button>
            </div>
        </header>
        <p class="hint">Start with a <code># Title</code>. Optional front matter sets <code>title</code>, <code>section</code>, <code>order</code> and <code>description</code>. Link to other pages with their file names, like <code>[Payouts](../creators/payouts.md)</code>.</p>
        <div class="docs-editor-panes">
            <div class="docs-editor-pane">
                <label for="docs-source">Markdown</label>
                <textarea id="docs-source" spellcheck="true"></textarea>
            </div>
            <div class="docs-editor-pane">
                <span class="docs-editor-label">Preview</span>
                <div class="surface panel markdown docs-body" id="docs-preview" aria-live="off"></div>
            </div>
        </div>
    </form>`;

    const form = main.querySelector('#docs-editor');
    const source = main.querySelector('#docs-source');
    const preview = main.querySelector('#docs-preview');
    const save = main.querySelector('#docs-save');
    source.value = isNew ? starter : page.markdown;

    let timer = 0;
    let latest = 0;

    const refresh = async () => {
        const ticket = ++latest;

        try {
            const result = await api('/api/v1/docs/preview', { method: 'POST', json: { slug, markdown: source.value } });

            if (ticket === latest)
                preview.innerHTML = result.html || '<p class="hint">Nothing to preview yet.</p>';
        } catch (error) {
            if (ticket === latest)
                preview.innerHTML = `<p class="hint">${esc(error.message)}</p>`;
        }
    };

    source.addEventListener('input', () => {
        clearTimeout(timer);
        timer = setTimeout(refresh, 250);
    });

    // Tab indents instead of leaving the editor, which matters for lists and code blocks.
    source.addEventListener('keydown', event => {
        if (event.key === 'Tab' && !event.shiftKey && !event.altKey && !event.ctrlKey && !event.metaKey) {
            event.preventDefault();
            source.setRangeText('    ', source.selectionStart, source.selectionEnd, 'end');
            source.dispatchEvent(new Event('input'));
        }
    });

    const dirty = () => source.value !== saved;

    window.addEventListener('beforeunload', event => {
        if (dirty())
            event.preventDefault();
    });

    form.addEventListener('keydown', event => {
        if ((event.metaKey || event.ctrlKey) && event.key === 's') {
            event.preventDefault();
            form.requestSubmit();
        }
    });

    form.addEventListener('submit', async event => {
        event.preventDefault();
        save.disabled = true;

        try {
            const result = await api('/api/v1/docs/pages/' + slug, { method: 'PUT', json: version ? { markdown: source.value, version } : { markdown: source.value } });
            saved = source.value;
            version = result.version;
            location.href = result.url;
        } catch (error) {
            toast(error.message, 'error');
            save.disabled = false;
        }
    });

    main.querySelector('#docs-delete')?.addEventListener('click', async () => {
        if (!confirm(`Delete “${page.title}”? This removes docs/${slug}.md.`))
            return;

        try {
            await api('/api/v1/docs/pages/' + slug, { method: 'DELETE', json: { version } });
            saved = source.value;
            location.href = '/docs';
        } catch (error) {
            toast(error.message, 'error');
        }
    });

    refresh();
    source.focus();
}
