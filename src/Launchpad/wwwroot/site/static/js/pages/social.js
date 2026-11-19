import { ago, api, app, auth, avatar, esc, icon, requireSignIn, setTitle, toast } from '../core.js';

const titles = { friends: 'Friends', chat: 'Messages', groups: 'Publishing groups', transfers: 'Ownership transfers', notifications: 'Notifications' };

export async function render(view) {
    setTitle(titles[view]);

    if (!auth.user) {
        app.innerHTML = requireSignIn(titles[view], 'Sign in to use friends, chat and publishing groups.');
        return;
    }

    app.innerHTML = `<div class="page-head"><div><h1>${titles[view]}</h1></div>
        <nav class="segmented" aria-label="Social"><a href="/friends" ${view === 'friends' ? 'aria-current="page"' : ''}>Friends</a><a href="/chat" ${view === 'chat' ? 'aria-current="page"' : ''}>Messages</a><a href="/groups" ${view === 'groups' ? 'aria-current="page"' : ''}>Groups</a><a href="/transfers" ${view === 'transfers' ? 'aria-current="page"' : ''}>Transfers</a></nav></div>
        <div id="social"></div>`;

    const root = app.querySelector('#social');
    const views = { friends, chat, groups, transfers, notifications };
    await views[view](root);
}

async function friends(root) {
    const data = await api('/api/v1/friends');
    const person = (u, actions) => `<div class="person">${avatar(u)}<div class="grow"><a href="/profile/${encodeURIComponent(u.id)}"><strong>${esc(u.name)}</strong></a><small>@${esc(u.username)}</small></div>${actions}</div>`;
    const statusLabel = { online: 'Online', in_game: 'In game', offline: 'Offline' };

    root.innerHTML = `<div class="social-layout">
        <div class="stack">
            <form class="surface panel" id="add-friend"><h2>Add a friend</h2><div class="row"><label class="sr-only" for="friend-user">Username or ID</label><input id="friend-user" required maxlength="100" placeholder="@username or ID"><button class="button">${icon('plus')}Add</button></div></form>
            <section class="surface panel"><h2>Room visibility</h2><p class="hint">Friends can see and join your Gorilla Tag room while Launchpad is connected.</p><select id="visibility" aria-label="Who can see your room"><option value="none">Nobody</option><option value="friends">Friends</option></select></section>
            <section class="surface panel"><h2>Requests</h2>${data.requests.map(u => person(u, `<button class="button small" data-accept="${esc(u.id)}">Accept</button><button class="button ghost small" data-remove="${esc(u.id)}">Decline</button>`)).join('') || '<p class="hint">No incoming requests.</p>'}
                ${data.sent.length ? `<div class="gap"></div><div class="filter-title">Sent</div>${data.sent.map(u => person(u, `<button class="button ghost small" data-remove="${esc(u.id)}">Cancel</button>`)).join('')}` : ''}</section>
        </div>
        <section class="surface panel"><div class="row"><h2>Your friends</h2><span class="spacer"></span><button class="button ghost small" id="refresh">Refresh</button></div>
            ${data.friends.map(u => person(u, `<span class="presence ${u.status}" title="${statusLabel[u.status]}"></span><span class="hint">${statusLabel[u.status]}${u.room_code ? ` · <code>${esc(u.room_code)}</code>` : ''}</span><button class="button subtle small" data-chat="${esc(u.id)}">${icon('chat')}</button><button class="button ghost small" data-remove="${esc(u.id)}" aria-label="Remove friend">${icon('x')}</button>`)).join('') || '<div class="empty"><h2>No friends yet</h2><p>Add someone by username or ID.</p></div>'}
        </section>
    </div>`;

    const reload = () => friends(root);
    const visibility = root.querySelector('#visibility');
    visibility.value = data.visibility;
    visibility.onchange = () => api('/api/v1/presence', { method: 'PUT', json: { visibility: visibility.value } }).then(() => toast('Room visibility saved.')).catch(e => toast(e.message, 'error'));

    root.querySelector('#add-friend').onsubmit = async event => {
        event.preventDefault();

        try {
            await api('/api/v1/friends', { method: 'POST', json: { user_id: root.querySelector('#friend-user').value } });
            toast('Friend request sent.');
            await reload();
        } catch (error) {
            toast(error.message, 'error');
        }
    };
    root.querySelectorAll('[data-accept]').forEach(b => b.onclick = () => api('/api/v1/friends/' + encodeURIComponent(b.dataset.accept), { method: 'PUT' }).then(reload).catch(e => toast(e.message, 'error')));
    root.querySelectorAll('[data-remove]').forEach(b => b.onclick = () => api('/api/v1/friends/' + encodeURIComponent(b.dataset.remove), { method: 'DELETE' }).then(reload).catch(e => toast(e.message, 'error')));
    root.querySelectorAll('[data-chat]').forEach(b => b.onclick = async () => {
        const result = await api('/api/v1/chat', { method: 'POST', json: { user_id: b.dataset.chat } });
        location.assign('/chat?c=' + encodeURIComponent(result.id));
    });
    root.querySelector('#refresh').onclick = reload;
}

async function chat(root) {
    let selected = new URLSearchParams(location.search).get('c');
    let conversations = [];
    let timer;
    let sending = false;

    root.innerHTML = `<div class="social-layout">
        <div class="stack">
            <form class="surface panel" id="start-chat"><h2>New conversation</h2><div class="row"><label class="sr-only" for="chat-user">Username or ID</label><input id="chat-user" required maxlength="100" placeholder="@username or ID"><button class="button">${icon('chat')}Start</button></div></form>
            <section class="surface panel"><h2>Conversations</h2><div id="conversations"></div></section>
        </div>
        <section class="surface panel chat-panel"><div id="chat-head"><h2>Select a conversation</h2></div><div class="messages" id="messages" aria-live="polite"></div>
            <form class="composer" id="composer" hidden><label class="sr-only" for="draft">Message</label><textarea id="draft" maxlength="4000" placeholder="Write a message… (Enter to send)"></textarea><button class="button">${icon('next')}</button></form>
        </section>
    </div>`;

    const list = root.querySelector('#conversations');
    const box = root.querySelector('#messages');
    const composer = root.querySelector('#composer');
    const draft = root.querySelector('#draft');

    const load = async () => {
        conversations = (await api('/api/v1/chat')).items;
        list.innerHTML = conversations.map(c => `<button class="conversation" data-id="${esc(c.id)}" aria-current="${c.id === selected}"><span class="avatar sm">${esc((c.name || '?')[0].toUpperCase())}</span><span><strong>${esc(c.name)}</strong><small>${c.group_id ? 'Group channel' : c.status === 'pending' ? (c.incoming ? 'Message request' : 'Awaiting approval') : c.status === 'declined' ? 'Declined' : 'Direct message'}</small></span></button>`).join('') || '<p class="hint">No conversations yet.</p>';
        list.querySelectorAll('[data-id]').forEach(b => b.onclick = () => {
            selected = b.dataset.id;
            history.replaceState(null, '', '/chat?c=' + encodeURIComponent(selected));
            box.innerHTML = '';
            load();
        });

        const c = conversations.find(x => x.id === selected);

        if (!c) {
            composer.hidden = true;
            return;
        }

        const messages = await api('/api/v1/chat/' + encodeURIComponent(c.id) + '/messages');
        root.querySelector('#chat-head').innerHTML = `<div class="row"><h2>${esc(c.name)}</h2>${c.status === 'pending' && c.incoming ? '<span class="spacer"></span><button class="button small" id="accept">Accept</button><button class="button ghost small" id="decline">Decline</button>' : ''}</div>`;
        root.querySelector('#accept')?.addEventListener('click', () => api('/api/v1/chat/' + c.id + '/accept', { method: 'POST' }).then(load));
        root.querySelector('#decline')?.addEventListener('click', () => api('/api/v1/chat/' + c.id + '/decline', { method: 'POST' }).then(load));

        const nearBottom = box.scrollHeight - box.scrollTop - box.clientHeight < 80;
        const html = messages.items.map(m => `<div class="message ${m.sender === auth.user.id ? 'own' : ''}">${avatar(m.author, 'sm')}<div class="message-body"><div class="bubble">${esc(m.body)}</div><time>${esc(ago(m.created))}</time></div></div>`).join('') || '<p class="hint">No messages yet.</p>';

        if (box.innerHTML !== html)
            box.innerHTML = html;

        if (nearBottom)
            box.scrollTop = box.scrollHeight;

        const canSend = c.status === 'accepted' || (c.status === 'pending' && !c.incoming && messages.items.length === 0);
        composer.hidden = !canSend;
        draft.placeholder = c.status === 'pending' ? 'Send one introductory message, then wait for approval.' : 'Write a message… (Enter to send)';
    };

    root.querySelector('#start-chat').onsubmit = async event => {
        event.preventDefault();

        try {
            selected = (await api('/api/v1/chat', { method: 'POST', json: { user_id: root.querySelector('#chat-user').value } })).id;
            history.replaceState(null, '', '/chat?c=' + encodeURIComponent(selected));
            await load();
        } catch (error) {
            toast(error.message, 'error');
        }
    };
    draft.onkeydown = event => {
        if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
            event.preventDefault();
            composer.requestSubmit();
        }
    };
    composer.onsubmit = async event => {
        event.preventDefault();

        if (sending || !draft.value.trim())
            return;

        sending = true;

        try {
            await api('/api/v1/chat/' + encodeURIComponent(selected) + '/messages', { method: 'POST', json: { body: draft.value } });
            draft.value = '';
            await load();
            box.scrollTop = box.scrollHeight;
        } catch (error) {
            toast(error.message, 'error');
        } finally {
            sending = false;
        }
    };

    const poll = async () => {
        if (!document.body.contains(root))
            return;

        if (document.visibilityState === 'visible')
            await load().catch(() => {});

        timer = setTimeout(poll, 5000);
    };

    await load();
    timer = setTimeout(poll, 5000);
}

async function groups(root) {
    const data = await api('/api/v1/groups');
    const profiles = await Promise.all(data.items.map(g => api('/api/v1/users/' + encodeURIComponent(g.id))));

    root.innerHTML = `<div class="social-layout">
        <form class="surface panel" id="create-group"><h2>Create a group</h2><p class="hint">Up to 20 members can publish and edit mods under one group profile, each with their own account.</p><div class="field"><label for="group-name">Group name</label><input id="group-name" required minlength="3" maxlength="60"></div><button class="button">${icon('plus')}Create group</button></form>
        <div class="stack">${data.items.map((g, i) => `<section class="surface panel">
            <div class="row"><h2><a href="/profile/${encodeURIComponent(g.id)}">${esc(g.name)}</a></h2><span class="pill mono">${profiles[i].members.length}/20</span><span class="pill">${esc(g.role)}</span><span class="spacer"></span><a class="button ghost small" href="/chat?c=${encodeURIComponent(g.conversation_id)}">${icon('chat')}Group chat</a></div>
            ${profiles[i].members.map(m => `<div class="person">${avatar(m, 'sm')}<div class="grow"><a href="/profile/${encodeURIComponent(m.id)}">${esc(m.name)}</a></div><span class="hint">${esc(m.role)}</span>${g.role === 'owner' && m.role !== 'owner' ? `<button class="button ghost small" data-remove="${esc(m.id)}" data-group="${esc(g.id)}">Remove</button>` : ''}</div>`).join('')}
            ${g.role === 'owner' ? `<form class="row" data-add="${esc(g.id)}"><input required placeholder="Add member by username or ID" aria-label="New member"><button class="button subtle">Add</button></form>` : ''}
        </section>`).join('') || '<div class="empty"><h2>No groups yet</h2><p>Create one to publish together.</p></div>'}</div>
    </div>`;

    const reload = () => groups(root);
    root.querySelector('#create-group').onsubmit = async event => {
        event.preventDefault();

        try {
            await api('/api/v1/groups', { method: 'POST', json: { name: root.querySelector('#group-name').value } });
            await reload();
        } catch (error) {
            toast(error.message, 'error');
        }
    };
    root.querySelectorAll('[data-add]').forEach(form => form.onsubmit = async event => {
        event.preventDefault();

        try {
            await api('/api/v1/groups/' + encodeURIComponent(form.dataset.add) + '/members', { method: 'POST', json: { user_id: form.querySelector('input').value } });
            await reload();
        } catch (error) {
            toast(error.message, 'error');
        }
    });
    root.querySelectorAll('[data-remove]').forEach(b => b.onclick = () => api('/api/v1/groups/' + encodeURIComponent(b.dataset.group) + '/members/' + encodeURIComponent(b.dataset.remove), { method: 'DELETE' }).then(reload).catch(e => toast(e.message, 'error')));
}

async function transfers(root) {
    const data = await api('/api/v1/mod-transfers');

    root.innerHTML = data.items.length ? `<div class="stack">${data.items.map(t => `<section class="surface panel row">
        ${icon('swap')}<div class="grow"><strong><a href="/view/${encodeURIComponent(t.guid)}">${esc(t.name)}</a></strong><div class="hint">${esc(t.source_name)} -> ${esc(t.target_name)} · ${esc(ago(t.created))}</div></div>
        <span class="spacer"></span>
        ${t.incoming ? `<button class="button small" data-action="accept" data-id="${esc(t.id)}">Accept</button><button class="button ghost small" data-action="decline" data-id="${esc(t.id)}">Decline</button>` : ''}
        ${t.outgoing ? `<button class="button ghost small" data-action="cancel" data-id="${esc(t.id)}">Cancel</button>` : ''}
    </section>`).join('')}</div>` : '<div class="empty"><h2>No pending transfers</h2><p>Start a transfer from a mod’s Manage page.</p></div>';

    root.querySelectorAll('[data-action]').forEach(b => b.onclick = async () => {
        try {
            await api('/api/v1/mod-transfers/' + encodeURIComponent(b.dataset.id) + '/' + b.dataset.action, { method: 'POST' });
            toast('Transfer updated.');
            await transfers(root);
        } catch (error) {
            toast(error.message, 'error');
        }
    });
}

async function notifications(root) {
    const data = await api('/api/v1/notifications');
    root.innerHTML = data.items.length
        ? `<div class="surface panel">${data.items.slice().reverse().map(n => `<div class="person">${icon('bell')}<div class="grow">${esc(n.body)}<small>${esc(ago(n.created))}</small></div></div>`).join('')}</div>`
        : '<div class="empty"><h2>All caught up</h2><p>New friend requests, messages and transfers show up here.</p></div>';
}
