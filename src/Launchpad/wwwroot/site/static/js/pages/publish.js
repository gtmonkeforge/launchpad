import { api, app, auth, brand, myGroups, capLabels, capTips, categories, esc, icon, initials, loaderLabels, modBadges, requireSignIn, setTitle, size, toast } from '../core.js';
import { fromMinor, storeConfig, toMinor } from '../store.js';
import { copy, fill } from '../store-copy.js';
import { tierManager } from '../tiers.js';

// Any GUID works; staff review it. It just has to fit in a URL.
const guidPattern = /^[^\s/\\?#%]{1,200}$/;
const versionPattern = /^[0-9][a-zA-Z0-9.+_-]{0,63}$/;
const MB = 1024 * 1024;

export async function render() {
    setTitle('Publish');

    if (!auth.user) {
        app.innerHTML = requireSignIn('Publish a mod', 'Create your page and ship releases. Your account is your author profile.');
        return;
    }

    const params = new URLSearchParams(location.search);
    const guid = params.get('guid');
    const channelName = params.get('channel') || 'stable';
    const mod = guid ? await api('/api/v1/mods/' + encodeURIComponent(guid) + '?channel=' + encodeURIComponent(channelName)) : null;

    const groups = await myGroups();
    const authorGroup = mod ? groups.find(g => g.id === mod.author.id) : null;

    if (mod && mod.author.id !== auth.user.id && !authorGroup) {
        app.innerHTML = '<div class="empty"><h2>Not your mod</h2><p>Only the author or members of its publishing group can manage this mod.</p></div>';
        return;
    }

    const groupProfiles = mod ? [] : await Promise.all(groups.map(g => api('/api/v1/users/' + encodeURIComponent(g.id)).catch(() => null)));
    const authors = mod
        ? [{ id: mod.author.id, name: mod.author.name, verified: mod.author.is_verified }]
        : [{ id: auth.user.id, name: auth.user.display_name, verified: !!auth.user.is_verified },
            ...groups.map((g, i) => ({ id: g.id, name: g.name, verified: !!groupProfiles[i]?.is_verified }))];
    const requested = params.get('as');
    const selectedAuthor = () => authors.find(a => a.id === app.querySelector('#author')?.value) || authors.find(a => a.id === requested) || authors[0];

    setTitle(mod ? 'Manage ' + mod.name : 'Publish');
    const links = mod?.links || [];

    app.innerHTML = `
        ${mod ? `<a class="breadcrumb" href="/view/${encodeURIComponent(mod.guid)}">${icon('back')}${esc(mod.name)}</a>` : ''}
        <div class="page-head"><div><h1>${mod ? esc(mod.name) : 'Publish a mod'}</h1><p>${mod ? 'Edit the listing, then ship a new release.' : 'Three steps: identity, page, release.'}</p></div></div>
        <div class="stepper" role="tablist" aria-label="Publishing steps">
            <button type="button" role="tab" data-step="0"><span>01</span><div><strong>Identity</strong><small>Name, GUID, visibility</small></div></button>
            <button type="button" role="tab" data-step="1"><span>02</span><div><strong>Page</strong><small>Description, images, links</small></div></button>
            <button type="button" role="tab" data-step="2"><span>03</span><div><strong>Release</strong><small>Version and downloads</small></div></button>
        </div>
        <div class="publish-layout">
            <form id="publish-form" novalidate>
                <section class="publish-step" data-panel="0">
                    <div class="surface panel">
                        <div class="field"><label for="name">Mod name</label><input id="name" required maxlength="100" placeholder="Your mod’s display name" value="${esc(mod?.name || '')}"></div>
                        <div class="field"><label for="guid">GUID <span>permanent</span></label><input id="guid" class="mono-input" required maxlength="200" placeholder="com.yourname.yourmod" value="${esc(mod?.guid || '')}" ${mod ? 'readonly' : ''}><p class="hint">Every release of this mod uses the same GUID. Staff check it when they review the mod.</p></div>
                        ${authors.length > 1 ? `<div class="field"><label for="author">Publish as</label><select id="author">${authors.map(a => `<option value="${esc(a.id)}" ${a.id === requested ? 'selected' : ''}>${esc(a.name)}${a.id === auth.user.id ? ' (you)' : ' · group'}</option>`).join('')}</select><p class="hint">Mods published as a group can be updated by every member of the group.</p></div>` : ''}
                        <div class="field" id="slug-field"><label for="slug">Custom URL <span>verified benefit</span></label><div class="row"><span class="hint mono">/view/</span><input id="slug" class="mono-input" maxlength="40" placeholder="your-mod" value="${esc(mod?.custom_slug || '')}">${mod ? '<button type="button" class="button subtle" id="save-slug">Save</button>' : ''}</div></div>
                        <div class="grid-2">
                            <div class="field"><label for="category">Category</label><select id="category">${categories.map(c => `<option ${(mod?.category || 'Gameplay') === c ? 'selected' : ''}>${c}</option>`).join('')}</select></div>
                            ${mod ? '' : '<div class="field"><label for="visibility">Visibility</label><select id="visibility"><option value="public">Public · reviewed</option><option value="private">Private · only you</option><option value="code">Requires a channel code</option></select></div>'}
                        </div>
                        <div class="field"><label for="source">Source code <span>optional</span></label><input id="source" type="url" maxlength="2000" placeholder="https://github.com/you/your-mod" value="${esc(mod?.source_url || '')}"></div>
                        ${mod ? '' : `<div class="field"><label class="check"><input type="checkbox" id="uses-injector">${esc(copy.injectorCheck)}</label><p class="hint">${esc(copy.injectorHint)}</p></div>`}
                    </div>
                    <div class="row sticky-actions"><span class="hint">Step 1 of 3</span><span class="spacer"></span><button type="button" class="button" data-go="1">Continue${icon('next')}</button></div>
                </section>

                <section class="publish-step" data-panel="1" hidden>
                    <div class="surface panel">
                        <div class="field"><div class="field-label">Description <span>Markdown</span></div>
                            <div class="row"><div class="editor-toolbar" id="toolbar">
                                <button type="button" data-md="**|**" aria-label="Bold"><b>B</b></button><button type="button" data-md="*|*" aria-label="Italic"><i>I</i></button>
                                <button type="button" data-md="## |" aria-label="Heading">H</button><button type="button" data-md="- |" aria-label="List">• List</button>
                                <button type="button" data-md="[|](https://)" aria-label="Link">Link</button><button type="button" data-md="\`|\`" aria-label="Code">Code</button>
                            </div><span class="spacer"></span><div class="segmented"><button type="button" aria-selected="true" data-editor="write">Write</button><button type="button" aria-selected="false" data-editor="preview">Preview</button></div></div>
                            <label class="sr-only" for="description">Description</label>
                            <textarea id="description" class="mono-input" required maxlength="30000" rows="12" placeholder="What does your mod do? Include install steps and anything players should know.">${esc(mod?.description || '')}</textarea>
                            <div class="rich-editor markdown" id="description-preview" hidden></div>
                        </div>
                    </div>
                    <div class="surface panel">
                        <h2>Images</h2>
                        <div class="grid-2">
                            <div class="field"><label for="logo">Logo <span>square · 5 MB</span></label><input id="logo" type="file" accept="image/png,image/jpeg,image/webp"><div class="thumb-row" id="logo-preview">${mod?.logo ? `<img class="square" src="${esc(mod.logo)}" alt="Current logo">` : ''}</div>${mod?.logo ? '<label class="check"><input type="checkbox" id="remove-logo">Remove logo</label>' : ''}</div>
                            <div class="field"><label for="banner">Banner <span>wide · 5 MB</span></label><input id="banner" type="file" accept="image/png,image/jpeg,image/webp"><div class="thumb-row" id="banner-preview">${mod?.banner ? `<img src="${esc(mod.banner)}" alt="Current banner">` : ''}</div>${mod?.banner ? '<label class="check"><input type="checkbox" id="remove-banner">Remove banner</label>' : ''}</div>
                        </div>
                        <div class="field"><label for="screenshots">Screenshots <span>up to 20</span></label><input id="screenshots" type="file" accept="image/png,image/jpeg,image/webp" multiple><div class="thumb-row" id="screenshots-preview">${(mod?.screenshots || []).map(s => `<img src="${esc(s)}" alt="">`).join('')}</div>${mod?.screenshots?.length ? '<p class="hint">New uploads replace the current set.</p><label class="check"><input type="checkbox" id="remove-screenshots">Remove all screenshots</label>' : ''}</div>
                    </div>
                    <div class="surface panel">
                        <h2>Capabilities</h2>
                        <div class="row">${Object.entries(capLabels).map(([key, label]) => `<label class="check" title="${esc(capTips[key])}"><input type="checkbox" data-cap="${key}" ${mod?.capabilities?.[key] ? 'checked' : ''}>${label}</label>`).join('')}</div>
                        <p class="hint">Declare automatic updates, extensions and networking so players know what to expect.</p>
                    </div>
                    <div class="surface panel">
                        <h2>Links <span class="hint">up to three, besides source code</span></h2>
                        ${[0, 1, 2].map(i => `<div class="link-row"><input data-link-label aria-label="Link ${i + 1} label" maxlength="80" placeholder="Discord server" value="${esc(links[i]?.label || '')}"><input data-link-url aria-label="Link ${i + 1} URL" type="url" maxlength="2000" placeholder="https://" value="${esc(links[i]?.url || '')}"><span></span></div>`).join('')}
                    </div>
                    <div class="row sticky-actions"><button type="button" class="button ghost" data-go="0">${icon('back')}Back</button><span class="spacer"></span>${mod ? `<button type="button" class="button subtle" id="save-page">${icon('check')}Save page</button>` : ''}<button type="button" class="button" data-go="2">Continue${icon('next')}</button></div>
                </section>

                <section class="publish-step" data-panel="2" hidden>
                    <p class="notice">Releases are immutable: versions, files and changelogs can’t be edited after publishing. Ship a new version to update downloads.</p>
                    <div class="surface panel">
                        <div class="grid-2">
                            <div class="field"><label for="version">Version</label><input id="version" class="mono-input" required maxlength="64" placeholder="1.0.0"><p class="hint">${mod?.current_version ? 'Current: v' + esc(mod.current_version) : 'The version in this release.'}</p></div>
                            <div class="field"><label for="channel">Release channel</label><select id="channel">${(mod?.channels || [{ name: 'stable', enabled: true }]).filter(c => c.enabled).map(c => `<option ${c.name === channelName ? 'selected' : ''}>${esc(c.name)}</option>`).join('')}</select><p class="hint">Stable releases also update the listing.</p></div>
                        </div>
                    </div>
                    <div class="surface panel">
                        <h2>Downloads <span class="hint">one DLL, one ZIP, or both per loader · 50 MB each</span></h2>
                        <div class="loader-cards">${Object.entries(loaderLabels).map(([loader, label]) => `<div class="loader-card dropzone" data-drop="${loader}"><strong>${label}</strong><input id="files-${loader}" type="file" accept=".dll,.zip" multiple aria-label="${label} downloads"><div class="file-chips" id="chips-${loader}"></div></div>`).join('')}</div>
                        <details class="field"><summary class="hint">Copy assets from a GitHub release</summary>
                            <div class="row"><input id="github-url" type="url" placeholder="https://github.com/owner/repo/releases/tag/v1.0.0"><select id="github-loader">${Object.entries(loaderLabels).map(([v, l]) => `<option value="${v}">${l}</option>`).join('')}</select><button type="button" class="button subtle" id="github-load">Load</button></div>
                            <div id="github-assets"></div><p class="hint" id="github-status" role="status"></p>
                        </details>
                    </div>
                    <div class="surface panel"><div class="field"><label for="notes">Changelog <span>optional</span></label><textarea id="notes" maxlength="10000" rows="4" placeholder="What’s new in this release?"></textarea></div></div>
                    <div class="error" id="publish-error" role="alert" hidden></div>
                    <div class="row sticky-actions"><button type="button" class="button ghost" data-go="1">${icon('back')}Back</button><span class="hint">Publishing as <strong id="publishing-as">${esc(selectedAuthor().name)}</strong></span><span class="spacer"></span><button type="submit" class="button" id="publish">${icon('rocket')}${mod ? 'Publish release' : 'Publish mod'}</button></div>
                </section>
            </form>
            <aside class="preview-pane" aria-label="Live preview"><span class="hint">Preview</span><div class="preview-frame" id="preview"></div></aside>
        </div>
        ${mod ? '<div id="manage"></div>' : ''}`;

    const form = app.querySelector('#publish-form');
    const description = form.querySelector('#description');
    const previewHtml = { value: mod?.description_html || '' };
    const objectUrls = {};

    setupSteps();
    setupEditor(description, previewHtml, () => preview(mod, form, previewHtml.value, objectUrls, selectedAuthor().name));
    setupImages(form, objectUrls, () => preview(mod, form, previewHtml.value, objectUrls, selectedAuthor().name));
    setupFiles();
    setupGitHub(form);
    form.addEventListener('input', () => preview(mod, form, previewHtml.value, objectUrls, selectedAuthor().name));

    const syncAuthor = () => {
        const author = selectedAuthor();
        app.querySelector('#slug-field').hidden = !author.verified;
        app.querySelector('#publishing-as').textContent = author.name;
        preview(mod, form, previewHtml.value, objectUrls, author.name);
    };

    app.querySelector('#author')?.addEventListener('change', syncAuthor);
    syncAuthor();

    form.onsubmit = event => {
        event.preventDefault();
        publish(mod, form);
    };

    app.querySelector('#save-page')?.addEventListener('click', () => savePage(mod, form));
    app.querySelector('#save-slug')?.addEventListener('click', async () => {
        try {
            await api('/api/v1/mods/' + encodeURIComponent(mod.guid) + '/url', { method: 'PATCH', json: { custom_slug: app.querySelector('#slug').value } });
            toast('Custom URL saved.');
        } catch (error) {
            toast(error.message, 'error');
        }
    });

    if (mod)
        manage(mod, mod.author.id === auth.user.id || authorGroup?.role === 'owner');
}

function setupSteps() {
    const tabs = [...app.querySelectorAll('[data-step]')];

    const select = index => {
        tabs.forEach((tab, i) => tab.setAttribute('aria-selected', String(i === index)));
        app.querySelectorAll('[data-panel]').forEach(panel => panel.hidden = Number(panel.dataset.panel) !== index);
        window.scrollTo({ top: 0, behavior: 'smooth' });
    };

    tabs.forEach((tab, i) => tab.onclick = () => select(i));
    app.querySelectorAll('[data-go]').forEach(button => button.onclick = () => select(Number(button.dataset.go)));
    select(0);
}

function setupEditor(textarea, previewHtml, refresh) {
    const box = app.querySelector('#description-preview');
    let timer;
    let ticket = 0;

    const render = async () => {
        const id = ++ticket;

        try {
            const result = await api('/api/v1/markdown-preview', { method: 'POST', json: { text: textarea.value } });

            if (id === ticket) {
                previewHtml.value = result.html;
                box.innerHTML = result.html;
                refresh();
            }
        } catch {
        }
    };

    textarea.addEventListener('input', () => {
        clearTimeout(timer);
        timer = setTimeout(render, 300);
    });

    app.querySelectorAll('[data-md]').forEach(button => button.onclick = () => {
        const [before, after] = button.dataset.md.split('|');
        const { selectionStart: start, selectionEnd: end, value } = textarea;
        const selected = value.slice(start, end);
        textarea.setRangeText(before + selected + after, start, end, 'end');
        textarea.focus();
        textarea.setSelectionRange(start + before.length, start + before.length + selected.length);
        textarea.dispatchEvent(new Event('input', { bubbles: true }));
    });

    app.querySelectorAll('[data-editor]').forEach(button => button.onclick = async () => {
        const showPreview = button.dataset.editor === 'preview';
        app.querySelectorAll('[data-editor]').forEach(b => b.setAttribute('aria-selected', String(b === button)));

        if (showPreview)
            await render();

        textarea.hidden = showPreview;
        box.hidden = !showPreview;
    });
}

function setupImages(form, objectUrls, refresh) {
    for (const key of ['logo', 'banner', 'screenshots']) {
        form.querySelector('#' + key).addEventListener('change', event => {
            const files = [...event.target.files].slice(0, 20);
            (objectUrls[key] || []).forEach(URL.revokeObjectURL);
            objectUrls[key] = files.map(file => URL.createObjectURL(file));
            form.querySelector('#' + key + '-preview').innerHTML = objectUrls[key].map(url => `<img ${key === 'logo' ? 'class="square"' : ''} src="${url}" alt="">`).join('');
            refresh();
        });
    }

    window.addEventListener('pagehide', () => Object.values(objectUrls).flat().forEach(URL.revokeObjectURL), { once: true });
}

function showFiles(loader) {
    const input = app.querySelector('#files-' + loader);
    app.querySelector('#chips-' + loader).innerHTML = [...input.files].map(f => `<span class="pill mono">${esc(f.name)} · ${size(f.size)}</span>`).join('');
}

function setupFiles() {
    for (const loader of Object.keys(loaderLabels)) {
        const zone = app.querySelector(`[data-drop="${loader}"]`);
        const input = zone.querySelector('input');
        input.onchange = () => showFiles(loader);

        zone.addEventListener('dragover', event => {
            event.preventDefault();
            zone.classList.add('over');
        });
        zone.addEventListener('dragleave', () => zone.classList.remove('over'));
        zone.addEventListener('drop', event => {
            event.preventDefault();
            zone.classList.remove('over');
            const transfer = new DataTransfer();
            [...event.dataTransfer.files].filter(f => /\.(dll|zip)$/i.test(f.name)).slice(0, 2).forEach(f => transfer.items.add(f));
            input.files = transfer.files;
            showFiles(loader);
        });
    }
}

function setupGitHub(form) {
    const status = app.querySelector('#github-status');
    const list = app.querySelector('#github-assets');
    const load = app.querySelector('#github-load');

    load.onclick = async () => {
        const url = app.querySelector('#github-url').value.trim();
        load.disabled = true;
        list.innerHTML = '';
        status.textContent = 'Loading release…';

        try {
            const release = await api('/api/v1/github-release', { method: 'POST', json: { url } });

            if (!release.assets.length)
                throw new Error('No DLL or ZIP assets of up to 50 MB were found.');

            status.textContent = 'Choose one DLL, one ZIP, or one of each.';
            list.innerHTML = release.assets.map((a, i) => `<label class="check"><input type="checkbox" value="${i}">${esc(a.name)} <span class="faint mono">${size(a.size)}</span></label>`).join('<br>') + '<div class="row"><button type="button" class="button subtle small" id="github-copy">Copy selected</button></div>';
            list.querySelector('#github-copy').onclick = () => copyAssets(form, url, release, list, status);
        } catch (error) {
            status.textContent = error.message;
        } finally {
            load.disabled = false;
        }
    };
}

async function copyAssets(form, url, release, list, status) {
    const selected = [...list.querySelectorAll('input:checked')].map(input => release.assets[Number(input.value)]);
    const kinds = selected.map(a => a.name.split('.').pop().toLowerCase());

    if (!selected.length || selected.length > 2 || new Set(kinds).size !== kinds.length) {
        status.textContent = 'Choose one DLL, one ZIP, or one of each.';
        return;
    }

    const loader = app.querySelector('#github-loader').value;
    const publish = app.querySelector('#publish');
    publish.disabled = true;
    status.textContent = 'Copying assets…';

    try {
        const transfer = new DataTransfer();

        for (const asset of selected) {
            const response = await fetch('/api/v1/github-release/asset', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-Token': auth.csrf_token }, body: JSON.stringify({ url, asset_id: asset.id }) });

            if (!response.ok)
                throw new Error((await response.json().catch(() => ({}))).error || 'Could not copy asset.');

            transfer.items.add(new File([await response.blob()], asset.name));
        }

        app.querySelector('#files-' + loader).files = transfer.files;
        showFiles(loader);

        const version = release.tag.replace(/^v(?=\d)/i, '');

        if (versionPattern.test(version) && !form.querySelector('#version').value)
            form.querySelector('#version').value = version;

        if (!form.querySelector('#notes').value)
            form.querySelector('#notes').value = release.notes.slice(0, 10000);

        status.textContent = 'Assets copied. Check the version and changelog, then publish.';
    } catch (error) {
        status.textContent = error.message;
    } finally {
        publish.disabled = false;
    }
}

function readLinks(form) {
    const labels = [...form.querySelectorAll('[data-link-label]')];
    const urls = [...form.querySelectorAll('[data-link-url]')];
    return labels.map((label, i) => ({ label: label.value.trim(), url: urls[i].value.trim() })).filter(l => l.label || l.url);
}

function capabilities(form) {
    return Object.fromEntries([...form.querySelectorAll('[data-cap]')].map(c => [c.dataset.cap, c.checked]));
}

function preview(mod, form, html, objectUrls, authorName) {
    const name = form.querySelector('#name').value || 'Your mod';
    const removed = id => form.querySelector('#remove-' + id)?.checked;
    const logo = objectUrls.logo?.[0] || (removed('logo') ? null : mod?.logo);
    const banner = objectUrls.banner?.[0] || (removed('banner') ? null : mod?.banner);
    const shot = objectUrls.screenshots?.[0] || (removed('screenshots') ? null : mod?.screenshots?.[0]);
    const caps = Object.entries(capabilities(form)).filter(([, on]) => on).map(([key]) => `<span class="pill accent">${capLabels[key]}</span>`).join('');

    app.querySelector('#preview').innerHTML = `
        ${banner ? `<img class="preview-banner" src="${esc(banner)}" alt="">` : ''}
        <div class="mod-hero">${logo ? `<img class="mod-icon" src="${esc(logo)}" alt="">` : `<span class="mod-icon">${esc(initials(name))}</span>`}
            <div><h1>${esc(name)}</h1><div class="meta-line"><span class="author-chip">${esc(authorName)}</span><span class="pill">${esc(form.querySelector('#category').value)}</span></div>${mod?.badges?.length ? `<div class="badges">${modBadges(mod)}</div>` : ''}</div></div>
        <div class="preview-body">
            ${shot ? `<img class="screenshot" src="${esc(shot)}" alt="">` : ''}
            <div class="markdown">${html || '<p class="hint">Your description appears here.</p>'}</div>
            ${caps ? `<div class="capabilities">${caps}</div>` : ''}
            <p class="hint mono">${esc(form.querySelector('#guid').value || 'com.yourname.yourmod')}</p>
        </div>`;
}

function validate(mod, form, release) {
    const name = form.querySelector('#name').value.trim();
    const guid = form.querySelector('#guid').value.trim();
    const description = form.querySelector('#description').value.trim();

    if (!name || !description || !guidPattern.test(guid))
        return 'Complete the mod name, a GUID (no spaces or / \\ ? # %) and a description before publishing.';

    const images = ['logo', 'banner', 'screenshots'].flatMap(id => [...form.querySelector('#' + id).files]);

    if (images.some(f => f.size > 5 * MB))
        return 'Each image must be 5 MB or smaller.';

    if (form.querySelector('#screenshots').files.length > 20)
        return 'Choose at most 20 screenshots.';

    if (!release)
        return null;

    if (!versionPattern.test(form.querySelector('#version').value.trim()))
        return 'Use a version such as 1.0.0.';

    const sets = Object.keys(loaderLabels).map(loader => [...app.querySelector('#files-' + loader).files]);

    if (!sets.some(files => files.length))
        return 'Attach downloads for at least one mod loader.';

    for (const files of sets) {
        const kinds = files.map(f => f.name.split('.').pop().toLowerCase());

        if (files.length > 2 || new Set(kinds).size !== kinds.length || kinds.some(k => k !== 'dll' && k !== 'zip'))
            return 'Each loader supports one DLL, one ZIP, or one of each.';

        if (files.some(f => f.size > 50 * MB))
            return 'Each download must be 50 MB or smaller.';
    }

    return null;
}

async function publish(mod, form) {
    const error = app.querySelector('#publish-error');
    const button = app.querySelector('#publish');
    const problem = validate(mod, form, true);
    error.hidden = true;

    if (problem) {
        error.textContent = problem;
        error.hidden = false;
        return;
    }

    const data = new FormData();
    const value = id => form.querySelector('#' + id)?.value.trim() || '';

    data.set('name', value('name'));
    data.set('guid', value('guid'));
    data.set('description', form.querySelector('#description').value);
    data.set('category', value('category'));
    data.set('source_url', value('source'));
    data.set('version', value('version'));
    data.set('channel', value('channel'));
    data.set('notes', form.querySelector('#notes').value);
    data.set('links', JSON.stringify(readLinks(form)));

    if (!mod) {
        data.set('visibility', value('visibility'));
        data.set('uses_injector', form.querySelector('#uses-injector')?.checked ? 'true' : 'false');
    }

    if (!mod && app.querySelector('#author'))
        data.set('author_id', value('author'));

    for (const [key, on] of Object.entries(capabilities(form)))
        data.set(key, on ? 'true' : 'false');

    for (const id of ['logo', 'screenshots'])
        data.set('remove_' + id, form.querySelector('#remove-' + id)?.checked ? 'true' : 'false');

    for (const id of ['logo', 'banner'])
        if (form.querySelector('#' + id).files[0])
            data.set(id, form.querySelector('#' + id).files[0]);

    for (const file of form.querySelector('#screenshots').files)
        data.append('screenshots', file);

    for (const loader of Object.keys(loaderLabels))
        for (const file of app.querySelector('#files-' + loader).files)
            data.append('downloads_' + loader, file);

    button.disabled = true;
    button.innerHTML = '<span class="spinner"></span>Uploading and scanning…';

    try {
        const result = await api(mod ? '/api/v1/mods/' + encodeURIComponent(mod.guid) + '/releases' : '/api/v1/mods', { method: 'POST', body: data });
        const slug = app.querySelector('#slug')?.value.trim();

        if (!mod && slug)
            await api('/api/v1/mods/' + encodeURIComponent(result.guid) + '/url', { method: 'PATCH', json: { custom_slug: slug } }).catch(e => toast(e.message, 'error'));

        location.assign(result.url);
    } catch (e) {
        error.textContent = e.message;
        error.hidden = false;
        button.disabled = false;
        button.innerHTML = `${icon('rocket')}${mod ? 'Publish release' : 'Publish mod'}`;
    }
}

async function savePage(mod, form) {
    const button = app.querySelector('#save-page');
    const problem = validate(mod, form, false);

    if (problem) {
        toast(problem, 'error');
        return;
    }

    const metadata = {
        name: form.querySelector('#name').value,
        description: form.querySelector('#description').value,
        category: form.querySelector('#category').value,
        source_url: form.querySelector('#source').value.trim() || null,
        links: readLinks(form),
        capabilities: capabilities(form),
    };
    const data = new FormData();

    for (const key of ['logo', 'banner', 'screenshots']) {
        for (const file of form.querySelector('#' + key).files)
            data.append(key, file);

        metadata['remove_' + key] = !!form.querySelector('#remove-' + key)?.checked;
    }

    data.set('metadata', JSON.stringify(metadata));
    button.disabled = true;

    try {
        const result = await api('/api/v1/mods/' + encodeURIComponent(mod.guid), { method: 'PATCH', body: data });
        toast(result.status === 'pending' && mod.status === 'approved' ? 'Page saved. Changes are waiting for review.' : 'Page saved.');
    } catch (error) {
        toast(error.message, 'error');
    } finally {
        button.disabled = false;
    }
}

async function manage(mod, canTransfer) {
    const root = app.querySelector('#manage');
    const base = '/api/v1/mods/' + encodeURIComponent(mod.guid);
    const channels = (await api(base + '/channels')).items;

    root.innerHTML = `
        <div class="section-title"><h2>Manage</h2></div>
        <div class="grid-2">
            <section class="surface panel" id="channels-panel">
                <h2>${icon('layers')} Release channels <span class="hint mono">${channels.length}/20</span></h2>
                <div class="field"><label for="manage-channel">Channel</label><select id="manage-channel">${channels.map(c => `<option>${esc(c.name)}</option>`).join('')}<option value="__new">+ New channel…</option></select></div>
                <form id="channel-form">
                    <div class="field" id="channel-name-field" hidden><label for="channel-name">Name</label><input id="channel-name" class="mono-input" maxlength="40" placeholder="beta"></div>
                    <div class="field"><label for="channel-notes">Notes</label><textarea id="channel-notes" maxlength="10000" rows="3"></textarea></div>
                    <div class="row"><label class="check"><input type="checkbox" id="channel-public">Public</label><label class="check"><input type="checkbox" id="channel-enabled">Enabled</label></div>
                    <div class="field" id="members-field"><label for="channel-members">Members <span>${esc(brand.name)} user IDs</span></label><textarea id="channel-members" class="mono-input" rows="2" placeholder="Comma-separated IDs"></textarea></div>
                    <div class="row"><button class="button" type="submit">Save channel</button><button class="button ghost" type="button" id="share-code">${icon('key')}Share code</button></div>
                    <div class="copy-field" id="share-output" hidden><input readonly aria-label="Share code"><button type="button" class="button subtle">${icon('copy')}</button></div>
                </form>
            </section>
            <div class="stack">
                <section class="surface panel">
                    <h2>${icon('eye')} Visibility</h2>
                    <div class="field"><label for="manage-visibility">Who can see this mod?</label><select id="manage-visibility"><option value="public">Public · reviewed</option><option value="private">Private · only you</option><option value="code">Requires a channel code</option></select></div>
                    <p class="hint">Public mods are reviewed. Private and code-only mods don’t need approval.</p>
                    <button class="button subtle" id="save-visibility">Save visibility</button>
                </section>
                <section class="surface panel" id="pricing-panel"><h2>${icon('tag')} ${esc(copy.pricingTitle)}</h2><p class="hint">Loading…</p></section>
                <section class="surface panel" id="tiers-panel" hidden></section>
                <section class="surface panel" ${canTransfer ? '' : 'hidden'}>
                    <h2>${icon('swap')} Transfer ownership</h2>
                    <p class="hint">Send this mod to another publisher or a publishing group. They must accept unless you own the destination.</p>
                    <form class="row" id="transfer-form"><input id="transfer-target" placeholder="@username or ID" required><button class="button ghost" type="submit">Request transfer</button></form>
                </section>
            </div>
        </div>`;

    const selector = root.querySelector('#manage-channel');
    const isPublic = root.querySelector('#channel-public');
    const show = () => {
        const channel = channels.find(c => c.name === selector.value);
        root.querySelector('#channel-name-field').hidden = !!channel;
        root.querySelector('#channel-notes').value = channel?.notes || '';
        isPublic.checked = channel?.public ?? true;
        root.querySelector('#channel-enabled').checked = channel?.enabled ?? true;
        root.querySelector('#channel-enabled').disabled = channel?.name === 'stable';
        root.querySelector('#channel-members').value = (channel?.members || []).join(', ');
        root.querySelector('#members-field').hidden = isPublic.checked;
        root.querySelector('#share-code').hidden = !channel;
        root.querySelector('#share-output').hidden = true;
    };

    selector.value = channels.some(c => c.name === mod.channel.name) ? mod.channel.name : 'stable';
    selector.onchange = show;
    isPublic.onchange = () => root.querySelector('#members-field').hidden = isPublic.checked;
    show();

    root.querySelector('#channel-form').onsubmit = async event => {
        event.preventDefault();
        const channel = channels.find(c => c.name === selector.value);
        const payload = {
            notes: root.querySelector('#channel-notes').value,
            public: isPublic.checked,
            enabled: root.querySelector('#channel-enabled').checked,
            members: root.querySelector('#channel-members').value.split(/[\s,]+/).filter(Boolean),
        };

        if (!channel)
            payload.name = root.querySelector('#channel-name').value.trim();

        try {
            await api(base + '/channels' + (channel ? '/' + encodeURIComponent(channel.name) : ''), { method: channel ? 'PATCH' : 'POST', json: payload });
            const url = new URL(location.href);
            url.searchParams.set('channel', channel?.name || payload.name);
            location.assign(url.pathname + url.search);
        } catch (error) {
            toast(error.message, 'error');
        }
    };

    root.querySelector('#share-code').onclick = async () => {
        try {
            const result = await api(base + '/channels/' + encodeURIComponent(selector.value) + '/share-code', { method: 'POST' });
            const output = root.querySelector('#share-output');
            output.hidden = false;
            output.querySelector('input').value = result.code;
            output.querySelector('button').onclick = () => navigator.clipboard.writeText(result.code).then(() => toast('Code copied.'));
        } catch (error) {
            toast(error.message, 'error');
        }
    };

    const visibility = root.querySelector('#manage-visibility');
    visibility.value = mod.visibility || 'public';
    root.querySelector('#save-visibility').onclick = async () => {
        try {
            const result = await api(base + '/visibility', { method: 'PATCH', json: { visibility: visibility.value } });
            toast(result.status === 'pending' ? 'Public visibility requested. The mod will be reviewed.' : 'Visibility saved.');
        } catch (error) {
            toast(error.message, 'error');
        }
    };

    pricing(mod, root.querySelector('#pricing-panel'));

    root.querySelector('#transfer-form').onsubmit = async event => {
        event.preventDefault();

        if (!confirm('Transfer ownership of ' + mod.name + '? You may lose access to manage it.'))
            return;

        try {
            const result = await api(base + '/transfer', { method: 'POST', json: { target_id: root.querySelector('#transfer-target').value } });
            toast(result.status === 'accepted' ? 'Ownership transferred.' : 'Transfer requested. Waiting for the recipient.');
        } catch (error) {
            toast(error.message, 'error');
        }
    };
}

async function pricing(mod, panel) {
    const config = await storeConfig();
    const personal = mod.author.id === auth.user.id;
    const payouts = config.enabled ? await api('/api/v1/payouts' + (personal ? '' : '?account=' + encodeURIComponent(mod.author.id))).catch(() => null) : null;
    const canSell = !!payouts?.charges_enabled;
    const payoutsUrl = '/settings/payouts' + (personal ? '' : '?account=' + encodeURIComponent(mod.author.id));
    const title = `<h2>${icon('tag')} ${esc(copy.pricingTitle)}</h2>`;
    const tiersPanel = app.querySelector('#tiers-panel');

    // Tier changes are re-read from the server so the list always matches what buyers see.
    const showTiers = async () => {
        const latest = await api('/api/v1/mods/' + encodeURIComponent(mod.guid));
        tiersPanel.hidden = false;
        await tierManager(tiersPanel, { guid: mod.guid, tiers: (latest.tiers || []).filter(t => t.scope === 'mod'), canSell, payoutsUrl, reload: showTiers });
    };

    if (config.enabled && mod.visibility === 'public')
        showTiers().catch(error => toast(error.message, 'error'));

    if (!config.enabled) {
        panel.innerHTML = `${title}<p class="hint">${esc(copy.storeDisabled)}</p>`;
        return;
    }

    panel.innerHTML = `${title}
        <div class="field"><label for="price">${esc(copy.pricingPrice)} <span>${esc(config.currency.toUpperCase())}</span></label>
            <input id="price" inputmode="decimal" value="${esc(fromMinor(mod.price_cents || 0, config.currency))}" ${canSell || mod.price_cents ? '' : 'disabled'}>
            <p class="hint">${esc(copy.pricingFreeHint)} ${esc(fill(copy.pricingFee, { brand: brand.name, fee: config.fee_percent }))}</p></div>
        ${canSell ? '' : `<p class="notice">${esc(copy.pricingNeedsPayouts)} <a href="${esc(payoutsUrl)}">${esc(copy.pricingSetupPayouts)}</a></p>`}
        ${mod.uses_injector
            ? `<p class="hint">${icon('lock')} ${esc(copy.pricingInjectorOn)}</p>`
            : `<div class="field"><label class="check"><input type="checkbox" id="pricing-injector">${esc(copy.injectorCheck)}</label><p class="hint">${esc(copy.injectorHint)}</p></div>`}
        <button class="button subtle" id="save-pricing">${esc(copy.pricingSave)}</button>`;

    panel.querySelector('#save-pricing').onclick = async () => {
        const payload = {};
        const price = toMinor(panel.querySelector('#price').value || '0', config.currency);
        const injector = panel.querySelector('#pricing-injector');

        if (!Number.isInteger(price))
            return toast(copy.pricingInvalid, 'error');

        if (price !== (mod.price_cents || 0))
            payload.price_cents = price;

        if (injector?.checked) {
            if (!confirm(copy.pricingInjectorConfirm))
                return;

            payload.uses_injector = true;
        }

        if (!Object.keys(payload).length)
            return toast(copy.pricingSaved);

        try {
            const result = await api('/api/v1/mods/' + encodeURIComponent(mod.guid) + '/pricing', { method: 'PUT', json: payload });
            toast(payload.uses_injector && result.status === 'pending' ? copy.pricingInjectorPending : copy.pricingSaved);
            mod.price_cents = result.price_cents;

            if (payload.uses_injector)
                location.reload();
        } catch (error) {
            toast(error.message, 'error');
        }
    };
}
