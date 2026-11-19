import { api, app, auth, avatar, brand, emptyState, esc, icon, modCard, pagination, setTitle, signInUrl, skeletonGrid, toast, userBadges } from '../core.js';
import { copy, fill } from '../store-copy.js';
import { bindSubscribe, renderLibrary, tierCard, tierManager } from '../tiers.js';
import { consumeReturn } from '../store.js';

export async function render(id) {
    let profile = await api('/api/v1/users/' + encodeURIComponent(id));
    const membership = auth.user && profile.is_group ? (await api('/api/v1/groups')).items.find(g => g.id === profile.id) : null;
    const own = auth.user?.id === profile.id;
    const editable = own || membership?.role === 'owner';
    setTitle(profile.display_name);

    const paint = () => {
        app.querySelector('#profile-hero').innerHTML = `
            ${avatar(profile, 'lg')}
            <div>
                <h1>${esc(profile.display_name)}</h1>
                <div class="row"><span class="handle">@${esc(profile.username)}</span>${userBadges(profile, true)}${profile.is_group ? '<span class="pill">Publishing group</span>' : ''}</div>
                ${profile.description ? `<p class="about">${esc(profile.description)}</p>` : ''}
                ${profile.links.length ? `<div class="profile-links">${profile.links.map(l => `<a class="button subtle small" href="${esc(l.url)}" target="_blank" rel="noopener noreferrer">${icon('link')}${esc(l.label)}</a>`).join('')}</div>` : ''}
                ${profile.members?.length ? `<div class="member-list">${profile.members.map(m => `<a class="author-chip" href="/profile/${encodeURIComponent(m.id)}">${avatar(m, 'sm')}${esc(m.name)}${m.role === 'owner' ? ' <span class="faint mono">owner</span>' : ''}</a>`).join('')}</div>` : ''}
            </div>
            <div class="row">
                ${editable ? `<button class="button subtle" id="edit-profile">${icon('edit')}${own ? 'Edit profile' : 'Edit group'}</button>` : ''}
                ${membership ? `<a class="button" href="/upload?as=${encodeURIComponent(profile.id)}">${icon('upload')}Publish as this group</a>` : ''}
                ${auth.user && !own && !profile.is_group ? `<button class="button subtle" id="add-friend">${icon('users')}Add friend</button><button class="button" id="message">${icon('chat')}Message</button>` : ''}
            </div>`;

        app.querySelector('#edit-profile')?.addEventListener('click', () => editor(profile, updated => {
            profile = updated;
            setTitle(profile.display_name);
            history.replaceState(null, '', profile.profile_url);
            paint();
        }));
        app.querySelector('#add-friend')?.addEventListener('click', async () => {
            try {
                await api('/api/v1/friends', { method: 'POST', json: { user_id: profile.id } });
                toast('Friend request sent.');
            } catch (error) {
                toast(error.message, 'error');
            }
        });
        app.querySelector('#message')?.addEventListener('click', async () => {
            try {
                const result = await api('/api/v1/chat', { method: 'POST', json: { user_id: profile.id } });
                location.assign('/chat?c=' + encodeURIComponent(result.id));
            } catch (error) {
                toast(error.message, 'error');
            }
        });
    };

    app.innerHTML = `
        <section class="surface profile-hero" id="profile-hero"></section>
        <section id="memberships" hidden></section>
        <div class="section-title"><h2>Published mods</h2><span class="pill mono">${profile.mod_count}</span></div>
        <div id="mods">${skeletonGrid(3)}</div>
        <div id="mod-pages"></div>
        ${own ? '<section class="surface panel" id="purchases"><p class="hint">Loading…</p></section>' : ''}`;
    paint();
    consumeReturn();

    memberships(profile, own).catch(error => toast(error.message, 'error'));

    if (own)
        renderLibrary(app.querySelector('#purchases')).then(() => {
            if (location.hash === '#purchases')
                app.querySelector('#purchases').scrollIntoView({ block: 'start' });
        }).catch(error => app.querySelector('#purchases').innerHTML = `<p class="error">${esc(error.message)}</p>`);

    let page = 1;

    const load = async () => {
        const data = await api('/api/v1/mods?author_id=' + encodeURIComponent(profile.id) + '&limit=12&page=' + page);
        const mods = app.querySelector('#mods');
        mods.innerHTML = data.items.length
            ? `<div class="mod-grid">${data.items.map(modCard).join('')}</div>`
            : emptyState('No mods yet', own || membership ? 'Published mods will appear here.' : 'This author hasn’t published anything yet.', own ? '<a class="button" href="/upload">Publish a mod</a>' : membership ? `<a class="button" href="/upload?as=${encodeURIComponent(profile.id)}">Publish as this group</a>` : '');
        pagination(app.querySelector('#mod-pages'), data.total, data.limit, page, next => {
            page = next;
            load();
        });
    };

    await load();
}

function editor(profile, saved) {
    const drawer = document.createElement('div');
    drawer.className = 'drawer';
    drawer.innerHTML = `<form class="drawer-panel" id="profile-form" aria-label="Edit profile">
        <div class="row"><h2>${profile.is_group ? 'Edit group' : 'Edit profile'}</h2><span class="spacer"></span><button type="button" class="icon-button" data-close aria-label="Close">${icon('x')}</button></div>
        ${profile.is_group ? `<div class="field"><label for="p-name">Group name</label><input id="p-name" required minlength="3" maxlength="60" value="${esc(profile.group_name || profile.display_name)}"><p class="hint">Used when the group has no handle.</p></div>` : ''}
        <div class="field"><label for="p-username">${profile.is_group ? 'Handle' : 'Public username'}</label><input id="p-username" maxlength="32" value="${esc(profile.public_username || '')}" placeholder="${profile.is_group ? 'your-group' : 'Choose a username'}"><p class="hint">3–32 letters, numbers, underscores, dots or hyphens.${profile.is_group ? ' When set, the handle is shown in place of the group name on mods and in apps, and people can transfer mods to the group by it.' : ''}</p></div>
        ${profile.is_verified ? `<div class="field"><label for="p-slug">Custom profile URL <span>verified</span></label><div class="row"><span class="hint mono">/profile/</span><input id="p-slug" class="mono-input" maxlength="40" value="${esc(profile.custom_slug || '')}" placeholder="${profile.is_group ? 'your-group' : 'your-name'}"></div></div>` : `<p class="hint">Custom profile URLs are available once ${profile.is_group ? 'the group is' : 'you are'} verified by ${esc(brand.name)} staff.</p>`}
        <div class="field"><label for="p-avatar">${profile.is_group ? 'Group picture' : 'Profile picture'}</label><input id="p-avatar" type="file" accept="image/png,image/jpeg,image/webp">
            <div class="crop" id="crop" hidden><canvas width="256" height="256" id="crop-canvas" aria-label="Crop preview"></canvas><div class="controls">
                <label>Zoom<input type="range" id="crop-zoom" min="1" max="4" step="0.05" value="1"></label>
                <label>Horizontal<input type="range" id="crop-x" min="-100" max="100" value="0"></label>
                <label>Vertical<input type="range" id="crop-y" min="-100" max="100" value="0"></label>
            </div></div>
            ${profile.avatar ? '<label class="check"><input type="checkbox" id="p-remove-avatar">Remove current picture</label>' : ''}</div>
        <div class="field"><label for="p-about">About <span>3,000 characters</span></label><textarea id="p-about" maxlength="3000" rows="5">${esc(profile.description)}</textarea></div>
        <div class="field"><div class="field-label">Links <span>up to five</span></div><div id="p-links"></div><button type="button" class="button ghost small" id="p-add-link">${icon('plus')}Add link</button></div>
        <div class="error" id="p-error" role="alert" hidden></div>
        <div class="row"><span class="spacer"></span><button type="button" class="button ghost" data-close>Cancel</button><button type="submit" class="button" id="p-save">Save profile</button></div>
    </form>`;
    document.body.append(drawer);

    const form = drawer.querySelector('form');
    const linkRows = drawer.querySelector('#p-links');
    const crop = cropper(drawer);
    const close = () => {
        crop.dispose();
        drawer.remove();
    };

    const addLink = (link = { label: '', url: '' }) => {
        const row = document.createElement('div');
        row.className = 'link-row';
        row.innerHTML = `<input class="l-label" aria-label="Label" maxlength="80" placeholder="GitHub" value="${esc(link.label)}" required><input class="l-url" aria-label="URL" type="url" maxlength="2000" placeholder="https://" value="${esc(link.url)}" required><button type="button" class="icon-button" aria-label="Remove link">${icon('trash')}</button>`;
        row.querySelector('button').onclick = () => {
            row.remove();
            limit();
        };
        linkRows.append(row);
        limit();
    };
    const limit = () => drawer.querySelector('#p-add-link').disabled = linkRows.children.length >= 5;

    profile.links.forEach(addLink);
    limit();
    drawer.querySelector('#p-add-link').onclick = () => addLink();
    drawer.querySelectorAll('[data-close]').forEach(b => b.onclick = close);
    drawer.addEventListener('click', event => {
        if (event.target === drawer)
            close();
    });
    drawer.addEventListener('keydown', event => {
        if (event.key === 'Escape')
            close();
    });
    drawer.querySelector('#p-username').focus();

    form.onsubmit = async event => {
        event.preventDefault();
        const error = drawer.querySelector('#p-error');
        const button = drawer.querySelector('#p-save');
        error.hidden = true;
        button.disabled = true;

        try {
            const remove = !!drawer.querySelector('#p-remove-avatar')?.checked;
            const payload = {
                public_username: drawer.querySelector('#p-username').value.trim() || null,
                description: drawer.querySelector('#p-about').value,
                links: [...linkRows.children].map(row => ({ label: row.querySelector('.l-label').value, url: row.querySelector('.l-url').value })),
                remove_avatar: remove,
            };

            if (profile.is_verified)
                payload.custom_slug = drawer.querySelector('#p-slug').value;

            if (profile.is_group)
                payload.display_name = drawer.querySelector('#p-name').value;

            const data = new FormData();
            data.set('profile', JSON.stringify(payload));
            const blob = remove ? null : await crop.blob();

            if (blob)
                data.set('avatar', blob, 'avatar.png');

            const updated = await api('/api/v1/users/' + encodeURIComponent(profile.id), { method: 'PUT', body: data });
            if (!profile.is_group)
                Object.assign(auth, await api('/api/v1/session'));

            close();
            saved(updated);
            toast(profile.is_group ? 'Group saved.' : 'Profile saved.');
        } catch (e) {
            error.textContent = e.message;
            error.hidden = false;
        } finally {
            button.disabled = false;
        }
    };
}

function cropper(root) {
    const input = root.querySelector('#p-avatar');
    const canvas = root.querySelector('#crop-canvas');
    const controls = ['#crop-zoom', '#crop-x', '#crop-y'].map(s => root.querySelector(s));
    let bitmap = null;

    const draw = () => {
        if (!bitmap)
            return;

        const edge = Math.min(bitmap.width, bitmap.height) / Number(controls[0].value);
        const x = (bitmap.width - edge) / 2 * (1 + Number(controls[1].value) / 100);
        const y = (bitmap.height - edge) / 2 * (1 + Number(controls[2].value) / 100);
        const context = canvas.getContext('2d');
        context.clearRect(0, 0, 256, 256);
        context.drawImage(bitmap, x, y, edge, edge, 0, 0, 256, 256);
    };

    controls.forEach(c => c.oninput = draw);
    input.onchange = async () => {
        bitmap?.close();
        bitmap = null;
        root.querySelector('#crop').hidden = true;
        const file = input.files[0];

        if (!file)
            return;

        if (file.size > 5 * 1024 * 1024 || !['image/png', 'image/jpeg', 'image/webp'].includes(file.type)) {
            toast('Choose a PNG, JPEG or WebP image up to 5 MB.', 'error');
            input.value = '';
            return;
        }

        bitmap = await createImageBitmap(file);
        controls.forEach((c, i) => c.value = i === 0 ? '1' : '0');
        root.querySelector('#crop').hidden = false;
        draw();
    };

    return {
        blob: () => bitmap ? new Promise(resolve => canvas.toBlob(resolve, 'image/png')) : Promise.resolve(null),
        dispose: () => bitmap?.close(),
    };
}

// Creator memberships: visitors see the tiers, the creator (or group owner) also gets the editor.
async function memberships(profile, own) {
    const section = app.querySelector('#memberships');
    const data = await api('/api/v1/users/' + encodeURIComponent(profile.id) + '/tiers');

    if (!data.items.length && !data.can_manage)
        return;

    section.hidden = false;
    section.innerHTML = `<div class="section-title"><h2>${esc(copy.membershipsTitle)}</h2></div>
        ${data.items.length ? `<p class="hint">${esc(fill(copy.membershipsIntro, { name: profile.display_name }))}</p>
            <div class="tier-grid">${data.items.map(t => tierCard(t, { canSubscribe: !data.can_manage, signInHref: auth.user ? null : signInUrl() })).join('')}</div>` : ''}
        ${data.can_manage ? '<div class="surface panel" id="membership-manager"></div>' : ''}`;
    bindSubscribe(section);

    if (data.can_manage) {
        const payoutsUrl = '/settings/payouts' + (own ? '' : '?account=' + encodeURIComponent(profile.id));
        await tierManager(section.querySelector('#membership-manager'), {
            ownerId: profile.id, tiers: data.items, mods: data.mods, linked: data.linked, canSell: data.can_sell, payoutsUrl,
            reload: () => memberships(profile, own),
        });
    }
}
