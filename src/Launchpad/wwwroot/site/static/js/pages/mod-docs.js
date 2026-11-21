import { ago, api, date, esc, icon, setTitle, toast } from '../core.js';

const slugPattern = /^[a-z0-9][a-z0-9-]{0,63}$/;

// The Docs tab: Markdown pages written by the mod's developers. basePath is the mod page's path, such as /view/my-mod,
// and navigate(slug) keeps the address bar on the page shown.
export async function docsPanel(root, mod, basePath, initialSlug, navigate) {
    const base = '/api/v1/mods/' + encodeURIComponent(mod.guid) + '/docs';
    let index;

    try {
        index = await api(base);
    } catch (error) {
        root.innerHTML = `<p class="hint">${esc(error.message)}</p>`;
        return;
    }

    let current = null;

    root.innerHTML = `
        ${index.can_edit && !index.enabled ? `<p class="notice">Only you can see this tab. Turn on Docs under <a href="/upload?guid=${encodeURIComponent(mod.guid)}#manage">Manage → Page tabs</a> to publish it.</p>` : ''}
        <div class="mod-docs">
            <aside class="mod-docs-nav" aria-label="${esc(mod.name)} documentation">
                <nav class="docs-nav" id="mdocs-nav"></nav>
                ${index.can_edit ? `<button type="button" class="button subtle small" id="mdocs-new">${icon('plus')}New page</button>` : ''}
            </aside>
            <div class="docs-main" id="mdocs-main"></div>
        </div>`;

    const nav = root.querySelector('#mdocs-nav');
    const main = root.querySelector('#mdocs-main');

    const drawNav = () => {
        nav.innerHTML = index.pages.length
            ? index.pages.map(p => `<a href="${esc(pageHref(p.slug))}" data-doc="${esc(p.slug)}" ${p.slug === current?.slug ? 'aria-current="page"' : ''}>${esc(p.title)}</a>`).join('')
            : '<p class="hint docs-empty">No pages yet.</p>';
        nav.querySelectorAll('[data-doc]').forEach(link => link.onclick = event => {
            event.preventDefault();
            show(link.dataset.doc);
        });
    };

    const pageHref = slug => basePath + '/docs/' + encodeURIComponent(slug) + location.search;

    const refreshIndex = async () => {
        index = await api(base);
        const count = document.querySelector('#doc-count');

        if (count)
            count.textContent = index.pages.length ? String(index.pages.length) : '';
    };

    async function show(slug) {
        if (!slug) {
            current = null;
            drawNav();
            main.innerHTML = index.can_edit
                ? `<div class="empty"><h2>Write the first page</h2><p>Docs are Markdown pages shown on this tab. Add install steps, configuration, an FAQ or an API reference.</p><button type="button" class="button" id="mdocs-first">${icon('edit')}New page</button></div>`
                : '<div class="empty"><h2>No documentation yet</h2><p>This mod’s developers haven’t written any pages.</p></div>';
            main.querySelector('#mdocs-first')?.addEventListener('click', () => edit(null));
            return;
        }

        try {
            current = await api(base + '/' + encodeURIComponent(slug));
        } catch (error) {
            current = null;
            drawNav();
            main.innerHTML = `<div class="empty"><h2>Page not found</h2><p>${esc(error.message)}</p></div>`;
            return;
        }

        navigate(current.slug);
        setTitle(`${current.title} · ${mod.name}`);
        drawNav();

        const position = index.pages.findIndex(p => p.slug === current.slug);
        const previous = index.pages[position - 1];
        const next = index.pages[position + 1];

        main.innerHTML = `<article class="surface panel mod-doc">
            <header class="doc-head"><h1>${esc(current.title)}</h1>
                ${index.can_edit ? `<div class="row">
                    <button type="button" class="button subtle small" id="mdocs-edit">${icon('edit')}Edit</button>
                    <button type="button" class="icon-button" id="mdocs-up" aria-label="Move page up" ${position <= 0 ? 'disabled' : ''}>${icon('back', 'up')}</button>
                    <button type="button" class="icon-button" id="mdocs-down" aria-label="Move page down" ${position >= index.pages.length - 1 ? 'disabled' : ''}>${icon('next', 'down')}</button>
                    <button type="button" class="icon-button" id="mdocs-delete" aria-label="Delete page">${icon('trash')}</button>
                </div>` : ''}
            </header>
            <div class="markdown">${current.html || '<p class="hint">This page is empty.</p>'}</div>
            <p class="hint"><time datetime="${esc(current.updated_at)}" title="${esc(date(current.updated_at))}">Updated ${esc(ago(current.updated_at))}</time></p>
        </article>
        ${previous || next ? `<nav class="doc-pager" aria-label="More pages">
            ${previous ? `<a class="surface" href="${esc(pageHref(previous.slug))}" data-doc="${esc(previous.slug)}"><small>Previous</small>${esc(previous.title)}</a>` : '<span></span>'}
            ${next ? `<a class="surface next" href="${esc(pageHref(next.slug))}" data-doc="${esc(next.slug)}"><small>Next</small>${esc(next.title)}</a>` : ''}
        </nav>` : ''}`;

        bindLinks(main, basePath + '/docs/', show);
        main.querySelectorAll('.doc-pager [data-doc]').forEach(link => link.onclick = event => {
            event.preventDefault();
            show(link.dataset.doc);
            root.scrollIntoView({ block: 'start', behavior: 'smooth' });
        });

        main.querySelector('#mdocs-edit')?.addEventListener('click', () => edit(current));
        main.querySelector('#mdocs-up')?.addEventListener('click', () => move(position, -1));
        main.querySelector('#mdocs-down')?.addEventListener('click', () => move(position, 1));
        main.querySelector('#mdocs-delete')?.addEventListener('click', remove);
    }

    async function move(position, by) {
        const order = index.pages.map(p => p.slug);
        [order[position], order[position + by]] = [order[position + by], order[position]];

        try {
            index.pages = (await api(base, { method: 'PUT', json: { order } })).pages;
            show(current.slug);
        } catch (error) {
            toast(error.message, 'error');
        }
    }

    async function remove() {
        if (!confirm(`Delete “${current.title}”? This can’t be undone.`))
            return;

        try {
            await api(base + '/' + encodeURIComponent(current.slug), { method: 'DELETE' });
            await refreshIndex();
            toast('Page deleted.');
            show(index.pages[0]?.slug);
        } catch (error) {
            toast(error.message, 'error');
        }
    }

    function edit(page) {
        main.innerHTML = `<form class="surface panel stack" id="mdocs-form" novalidate>
            <h2>${page ? 'Edit page' : 'New page'}</h2>
            <div class="grid-2">
                <div class="field"><label for="mdocs-title">Title</label><input id="mdocs-title" required maxlength="120" value="${esc(page?.title || '')}" placeholder="Getting started"></div>
                <div class="field"><label for="mdocs-slug">Address <span>${page ? '' : 'optional'}</span></label><input id="mdocs-slug" class="mono-input" maxlength="64" spellcheck="false" autocomplete="off" value="${esc(page?.slug || '')}" placeholder="${page ? '' : 'made from the title'}"></div>
            </div>
            <div class="field">
                <div class="row"><div class="field-label">Content <span>Markdown</span></div><span class="spacer"></span><div class="segmented"><button type="button" aria-selected="true" data-mode="write">Write</button><button type="button" aria-selected="false" data-mode="preview">Preview</button></div></div>
                <label class="sr-only" for="mdocs-body">Content</label>
                <textarea id="mdocs-body" class="mono-input" rows="18" maxlength="100000" placeholder="# Installation&#10;&#10;Explain how to install and use your mod.">${esc(page?.body || '')}</textarea>
                <div class="rich-editor markdown" id="mdocs-preview" hidden></div>
                <p class="hint">Link to another page with its address, like <code>[Configuration](configuration)</code>.</p>
            </div>
            <div class="row"><span class="spacer"></span><button type="button" class="button ghost" id="mdocs-cancel">Cancel</button><button type="submit" class="button">${icon('check')}${page ? 'Save page' : 'Create page'}</button></div>
        </form>`;

        const form = main.querySelector('#mdocs-form');
        const body = form.querySelector('#mdocs-body');
        const preview = form.querySelector('#mdocs-preview');

        form.querySelector('#mdocs-title').focus();
        form.querySelectorAll('[data-mode]').forEach(button => button.onclick = async () => {
            const previewing = button.dataset.mode === 'preview';
            form.querySelectorAll('[data-mode]').forEach(b => b.setAttribute('aria-selected', String(b === button)));

            if (previewing) {
                try {
                    preview.innerHTML = (await api('/api/v1/markdown-preview', { method: 'POST', json: { text: body.value } })).html || '<p class="hint">Nothing to preview.</p>';
                } catch (error) {
                    preview.innerHTML = `<p class="hint">${esc(error.message)}</p>`;
                }
            }

            body.hidden = previewing;
            preview.hidden = !previewing;
        });

        form.querySelector('#mdocs-cancel').onclick = () => show(page?.slug || current?.slug || index.pages[0]?.slug);

        form.onsubmit = async event => {
            event.preventDefault();
            const title = form.querySelector('#mdocs-title').value.trim();
            const slug = form.querySelector('#mdocs-slug').value.trim().toLowerCase();

            if (!title)
                return toast('Give the page a title.', 'error');

            if ((page || slug) && !slugPattern.test(slug))
                return toast('Addresses use lowercase letters, digits and hyphens, like getting-started.', 'error');

            const submit = form.querySelector('[type=submit]');
            submit.disabled = true;

            try {
                let saved;

                if (page) {
                    const changes = {};

                    if (title !== page.title)
                        changes.title = title;

                    if (slug !== page.slug)
                        changes.slug = slug;

                    if (body.value !== page.body)
                        changes.body = body.value;

                    saved = Object.keys(changes).length ? await api(base + '/' + encodeURIComponent(page.slug), { method: 'PATCH', json: changes }) : page;
                } else {
                    saved = await api(base, { method: 'POST', json: { title, slug, body: body.value } });
                }

                await refreshIndex();
                toast(page ? 'Page saved.' : 'Page created.');
                show(saved.slug);
            } catch (error) {
                toast(error.message, 'error');
                submit.disabled = false;
            }
        };
    }

    root.querySelector('#mdocs-new')?.addEventListener('click', () => edit(null));
    await show(initialSlug || index.pages[0]?.slug);
}

// Links to other pages of these docs stay on the tab; everything else opens in a new tab like the mod description.
function bindLinks(container, docsRoot, show) {
    container.querySelectorAll('.markdown a[href]').forEach(link => {
        const url = new URL(link.getAttribute('href'), location.origin + docsRoot);

        if (url.origin === location.origin && url.pathname.startsWith(docsRoot) && !url.pathname.slice(docsRoot.length).includes('/')) {
            link.href = url.pathname + location.search + url.hash;
            link.onclick = event => {
                event.preventDefault();
                show(decodeURIComponent(url.pathname.slice(docsRoot.length)));
            };
            return;
        }

        link.target = '_blank';
        link.rel = 'noopener noreferrer';
    });
}
