import { ago, api, auth, avatar, brand, esc, icon, onOutside, signInUrl, toast, userBadges } from './core.js';
import { openDonate } from './store.js';
import { copy, fill } from './store-copy.js';

const quips = [
    'Ever heard of MonkeModManager?',
    'Do NOT install OpSec from MonkeForge',
    "Ask me anything. I'll do everything.",
    "BBQ wings 10 o' clock",
    'ooo ooo aaa aaa',
    "Evan Burger's homepage",
];

const navItems = [
    ['/browse', 'Browse', path => path === '/browse' || path.startsWith('/view/')],
    ['/upload', 'Publish', path => path === '/upload'],
    ['/software', 'Software', path => path.startsWith('/software')],
];

export function renderHeader() {
    const path = location.pathname;
    const header = document.querySelector('.header-inner');
    const query = new URLSearchParams(location.search).get('q') || '';

    header.innerHTML = `
        <button class="icon-button menu-toggle" id="menu-toggle" aria-label="Open navigation" aria-expanded="false">${icon('menu')}</button>
        <a class="brand" href="/browse"><span class="brand-mark" aria-hidden="true"></span><span>${esc(brand.name)}</span></a>
        <nav class="main-nav" id="main-nav" aria-label="Main">
            ${navItems.map(([href, label, active]) => `<a href="${href}" ${active(path) ? 'aria-current="page"' : ''}>${label}</a>`).join('')}
            ${auth.user?.is_admin ? `<a href="/admin" ${path === '/admin' ? 'aria-current="page"' : ''}>Admin</a>` : ''}
        </nav>
        <form class="header-search" id="header-search" role="search" action="/browse">
            ${icon('search')}
            <label class="sr-only" for="header-q">Search mods</label>
            <input id="header-q" name="q" type="search" placeholder="Search mods, authors, GUIDs" value="${path === '/browse' ? esc(query) : ''}" maxlength="200" autocomplete="off">
            <kbd>/</kbd>
        </form>
        <a class="icon-button mobile-search" href="/browse" aria-label="Search mods">${icon('search')}</a>
        <div class="header-actions">${auth.user ? signedInActions() : signedOutActions()}</div>`;

    const toggle = header.querySelector('#menu-toggle');
    toggle.onclick = () => {
        const nav = header.querySelector('#main-nav');
        nav.classList.toggle('open');
        toggle.setAttribute('aria-expanded', String(nav.classList.contains('open')));
    };

    document.addEventListener('keydown', event => {
        const typing = ['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement?.tagName) || document.activeElement?.isContentEditable;

        if (event.key === '/' && !typing) {
            event.preventDefault();
            header.querySelector('#header-q').focus();
        }
    });

    bindUserMenu(header);
    bindNotifications(header);
}

function signedOutActions() {
    return `<div class="menu-wrap">
        <button class="icon-button" id="guest-menu-button" aria-label="Site settings" aria-haspopup="menu" aria-expanded="false">${icon('settings')}</button>
    </div>
    <a class="button small" href="${signInUrl()}">Sign in</a>`;
}

function signedInActions() {
    return `<div class="menu-wrap">
        <button class="icon-button" id="notifications-button" aria-label="Notifications" aria-haspopup="true" aria-expanded="false">${icon('bell')}<span class="dot-badge" id="notification-count" hidden></span></button>
    </div>
    <div class="menu-wrap">
        <button class="user-button" id="user-menu-button" aria-label="Account menu" aria-haspopup="menu" aria-expanded="false">${avatar(auth.user)}</button>
    </div>`;
}

function userMenu() {
    const user = auth.user;

    if (!user) {
        return `<div class="dropdown" role="menu">
            <div class="label">Site</div>
            <a role="menuitem" href="/settings">${icon('palette')}Appearance</a>
            <a role="menuitem" href="/software">${icon('rocket')}Get the apps</a>
            <hr>
            <a role="menuitem" href="${signInUrl()}">${icon('user')}Sign in</a>
            ${auth.email_signup ? `<a role="menuitem" href="/signup">${icon('plus')}Create an account</a>` : ''}
        </div>`;
    }

    return `<div class="dropdown" role="menu">
        <div class="dropdown-head">${avatar(user)}<div><strong>${esc(user.display_name)}</strong><small>@${esc(user.username)}</small><div>${userBadges(user, true)}</div></div></div>
        <a role="menuitem" href="${esc(user.profile_url)}">${icon('user')}Your profile</a>
        <a role="menuitem" href="/browse?mine=true">${icon('box')}Your mods</a>
        <a role="menuitem" href="/upload">${icon('upload')}Publish a mod</a>
        <hr>
        <div class="label">Account</div>
        <a role="menuitem" href="/settings">${icon('settings')}Settings</a>
        <a role="menuitem" href="${esc(user.profile_url)}#purchases">${icon('tray')}${esc(copy.purchasesNav)}</a>
        <a role="menuitem" href="/settings/payouts">${icon('wallet')}${esc(copy.payoutsNav)}</a>
        <a role="menuitem" href="/settings/security">${icon('lock')}Sign-in & security</a>
        <a role="menuitem" href="/settings/keys">${icon('key')}Access keys</a>
        <a role="menuitem" href="/settings/channels">${icon('lock')}Unlock a channel</a>
        <a role="menuitem" href="/transfers">${icon('swap')}Ownership transfers</a>
        <hr>
        <div class="label">Social</div>
        <a role="menuitem" href="/friends">${icon('users')}Friends</a>
        <a role="menuitem" href="/chat">${icon('chat')}Messages</a>
        <a role="menuitem" href="/groups">${icon('layers')}Publishing groups</a>
        ${user.is_admin ? `<hr><a role="menuitem" href="/admin">${icon('admin')}Admin panel<span class="meta">${user.is_root ? 'root' : 'admin'}</span></a>` : ''}
        <hr>
        <button role="menuitem" class="item danger-item" id="sign-out">${icon('logout')}Sign out</button>
    </div>`;
}

function bindUserMenu(header) {
    const button = header.querySelector('#user-menu-button') || header.querySelector('#guest-menu-button');

    if (!button)
        return;

    button.onclick = () => {
        const wrap = button.parentElement;
        const open = wrap.querySelector('.dropdown');

        if (open) {
            open.remove();
            button.setAttribute('aria-expanded', 'false');
            return;
        }

        wrap.insertAdjacentHTML('beforeend', userMenu());
        button.setAttribute('aria-expanded', 'true');
        const menu = wrap.querySelector('.dropdown');
        menu.querySelector('a, button')?.focus();

        const close = () => {
            menu.remove();
            button.setAttribute('aria-expanded', 'false');
        };

        const stop = onOutside(wrap, close);
        menu.addEventListener('keydown', event => {
            if (event.key === 'Escape') {
                stop();
                close();
                button.focus();
            }
        });

        menu.querySelector('#sign-out')?.addEventListener('click', async () => {
            try {
                await api('/api/v1/logout', { method: 'POST' });
                location.assign('/browse');
            } catch (error) {
                toast(error.message, 'error');
            }
        });
    };
}

function notificationLink(item) {
    switch (item.kind) {
        case 'friend_request':
        case 'friend_accepted':
            return '/friends';
        case 'message_request':
        case 'chat_message':
            return '/chat?c=' + encodeURIComponent(item.target);
        case 'group_member':
            return '/groups';
        case 'mod_transfer':
            return '/transfers';
        case 'purchase':
            return '/view/' + encodeURIComponent(item.target);
        case 'subscription':
        case 'dispute':
            return '/profile#purchases';
        case 'dispute_staff':
            return '/admin';
        case 'mod_review':
            return '/view/' + encodeURIComponent(item.target);
        case 'sale_refund':
            return '/settings/payouts';
        case 'payout_privacy':
            return '/settings/payouts?account=' + encodeURIComponent(item.target);
        default:
            return '#';
    }
}

function bindNotifications(header) {
    const button = header.querySelector('#notifications-button');

    if (!button)
        return;

    let items = [];
    const count = header.querySelector('#notification-count');

    const refresh = async () => {
        try {
            items = (await api('/api/v1/notifications')).items;
            count.hidden = items.length === 0;
            count.textContent = items.length > 99 ? '99+' : String(items.length);
        } catch {
            count.hidden = true;
        }
    };

    refresh();
    setInterval(() => {
        if (document.visibilityState === 'visible')
            refresh();
    }, 30000);

    button.onclick = () => {
        const wrap = button.parentElement;

        if (wrap.querySelector('.dropdown')) {
            wrap.querySelector('.dropdown').remove();
            button.setAttribute('aria-expanded', 'false');
            return;
        }

        wrap.insertAdjacentHTML('beforeend', `<div class="dropdown">
            <div class="dropdown-head"><strong>Notifications</strong><span class="spacer"></span>${items.length ? '<button class="button ghost small" id="mark-read">Mark all read</button>' : ''}</div>
            <hr>
            <div class="notification-list">${items.length
                ? items.slice().reverse().map(n => `<a class="notification" href="${esc(notificationLink(n))}">${esc(n.body)}<time>${esc(ago(n.created))}</time></a>`).join('')
                : '<p class="hint">You’re all caught up.</p>'}</div>
        </div>`);
        button.setAttribute('aria-expanded', 'true');

        const menu = wrap.querySelector('.dropdown');
        onOutside(wrap, () => {
            menu.remove();
            button.setAttribute('aria-expanded', 'false');
        });

        menu.querySelector('#mark-read')?.addEventListener('click', async () => {
            try {
                await api('/api/v1/notifications/read', { method: 'POST', json: { through: Math.max(...items.map(n => n.id)) } });
                menu.remove();
                await refresh();
            } catch (error) {
                toast(error.message, 'error');
            }
        });
    };
}

export function renderFooter() {
    document.querySelector('#site-footer').innerHTML = `<div class="footer-inner">
        <a class="brand" href="/browse"><span class="brand-mark" aria-hidden="true"></span><span>${esc(brand.name)}</span></a>
        <span class="quip">${esc(quips[Math.floor(Math.random() * quips.length)])}</span>
        <span class="spacer"></span>
        <nav aria-label="Footer">
            <a href="/software/launchpad">Launchpad</a>
            <a href="/software/cli">CLI</a>
            <a href="/software/openforge">OpenForge</a>
            <a href="/docs">Docs</a>
            <a href="/terms">Terms</a>
            <a href="/privacy">Privacy</a>
            <button type="button" class="footer-link" id="support-platform">${esc(fill(copy.footerSupport, { brand: brand.name }))}</button>
        </nav>
        <span>Community made. Not affiliated with Another Axiom.</span>
        <button type="button" class="version-label" id="version-label" aria-haspopup="dialog" hidden></button>
    </div>`;

    document.querySelector('#support-platform').onclick = () => openDonate({ recipient: 'platform', name: brand.name });
    bindVersion();
}

async function bindVersion() {
    const label = document.querySelector('#version-label');
    let info;

    try {
        info = await api('/api/v2/version');
    } catch {
        return;
    }

    label.textContent = 'v' + info.openforge;
    label.hidden = false;
    label.onclick = () => {
        const dialog = document.createElement('dialog');
        dialog.className = 'about-dialog';
        dialog.setAttribute('aria-labelledby', 'about-title');
        dialog.innerHTML = `
            <h2 id="about-title">About OpenForge</h2>
            <dl class="details-list">
                <div><dt>OpenForge</dt><dd class="mono">${esc(info.openforge)}${info.commit ? ` <span class="faint">(${esc(info.commit)})</span>` : ''}</dd></div>
                <div><dt>.NET</dt><dd class="mono">${esc(info.dotnet)}</dd></div>
                <div><dt>License</dt><dd><a href="${esc(info.license.url)}" target="_blank" rel="noopener noreferrer">${esc(info.license.name)}</a></dd></div>
            </dl>
            <p class="hint">OpenForge is free software: you can redistribute and modify it under the terms of the ${esc(info.license.spdx)} license. <a href="https://github.com/gtmonkeforge/openforge" target="_blank" rel="noopener noreferrer">Source code</a></p>
            <form method="dialog" class="row"><span class="spacer"></span><button class="button subtle">Close</button></form>`;
        dialog.addEventListener('close', () => dialog.remove());
        dialog.addEventListener('click', event => {
            if (event.target === dialog)
                dialog.close();
        });
        document.body.append(dialog);
        dialog.showModal();
    };
}
