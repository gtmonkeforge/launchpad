import { copy as storeCopy } from './store-copy.js';

export const app = document.querySelector('#app');
export const boot = JSON.parse(document.querySelector('#boot')?.textContent || '{}');
export const brand = boot.brand || { name: 'MonkeForge', tint_favicon: true };
export const auth = { user: null, csrf_token: '', oauth_configured: false, email_signup: false, mail_configured: false };

export const loaderLabels = { bepinex5: 'BepInEx 5', bepinex6: 'BepInEx 6', melonloader: 'MelonLoader' };
export const categories = ['Gameplay', 'Cosmetic', 'Utilities', 'Accessibility', 'Other'];
export const categoryIcons = { Gameplay: 'game', Cosmetic: 'palette', Utilities: 'tool', Accessibility: 'access', Other: 'box' };
export const capLabels = { auto_update: 'Auto-updating', extensions: 'Extensions', internet: 'Networking' };
export const capTips = {
    auto_update: 'This mod can automatically update itself to the latest version.',
    extensions: 'This mod can be extended by user-generated content (UGC).',
    internet: 'This mod sends data over the network.',
};
export const DEFAULT_ACCENT = '#dc5c3b';

const paths = {
    search: '<circle cx="11" cy="11" r="6.5"/><path d="m16 16 4.5 4.5"/>',
    download: '<path d="M12 4v11m-4.5-4.5L12 15l4.5-4.5M5 19h14"/>',
    plus: '<path d="M12 5v14M5 12h14"/>',
    grid: '<rect x="4" y="4" width="6.5" height="6.5" rx="1.5"/><rect x="13.5" y="4" width="6.5" height="6.5" rx="1.5"/><rect x="4" y="13.5" width="6.5" height="6.5" rx="1.5"/><rect x="13.5" y="13.5" width="6.5" height="6.5" rx="1.5"/>',
    game: '<path d="M7 8h10a4 4 0 0 1 4 4v1.5a3.5 3.5 0 0 1-6.2 2.2L14 15h-4l-.8.7A3.5 3.5 0 0 1 3 13.5V12a4 4 0 0 1 4-4Z"/><path d="M8 11v3m-1.5-1.5h3M16 12h.01M18 13.5h.01"/>',
    palette: '<path d="M12 3a9 9 0 1 0 0 18c1.1 0 1.7-.9 1.4-1.9-.4-1.2.4-2.1 1.6-2.1H17a4 4 0 0 0 4-4c0-5.5-4-10-9-10Z"/><circle cx="7.5" cy="11" r="1"/><circle cx="10" cy="7" r="1"/><circle cx="15" cy="7.5" r="1"/>',
    tool: '<path d="M14.5 6.5a4 4 0 0 0 5 5L12 19a2.1 2.1 0 0 1-3-3l7.5-7.5Z"/><path d="m9 9-5-5M4 9l5-5"/>',
    access: '<circle cx="12" cy="4.5" r="1.8"/><path d="M5 8.5h14M12 8.5V14m0 0-4 6.5m4-6.5 4 6.5"/>',
    box: '<path d="m12 3 8 4.5v9L12 21l-8-4.5v-9Z"/><path d="m4 7.5 8 4.5 8-4.5M12 12v9"/>',
    code: '<path d="m8 7-5 5 5 5m8-10 5 5-5 5M14 4l-4 16"/>',
    back: '<path d="M15 5 8 12l7 7"/>',
    next: '<path d="m9 5 7 7-7 7"/>',
    chevron: '<path d="m6 9 6 6 6-6"/>',
    user: '<circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1Z"/>',
    key: '<circle cx="8" cy="15" r="4"/><path d="m11 12 9-9m-3 3 3 3m-6 0 2 2"/>',
    lock: '<rect x="4.5" y="10.5" width="15" height="10" rx="2.5"/><path d="M8 10.5V7a4 4 0 0 1 8 0v3.5"/>',
    users: '<circle cx="9" cy="8" r="3.5"/><path d="M2.5 20a6.5 6.5 0 0 1 13 0M16 4.5a3.5 3.5 0 0 1 0 7M18.5 14a6 6 0 0 1 3 6"/>',
    chat: '<path d="M20 12a8 8 0 0 1-11.6 7.1L4 20l1-4.2A8 8 0 1 1 20 12Z"/>',
    layers: '<path d="m12 3 9 5-9 5-9-5 9-5Z"/><path d="m3 13 9 5 9-5"/>',
    swap: '<path d="M7 7h13m0 0-4-4m4 4-4 4M17 17H4m0 0 4 4m-4-4 4-4"/>',
    shield: '<path d="M12 3 4.5 6v5.5c0 4.6 3.2 8.3 7.5 9.5 4.3-1.2 7.5-4.9 7.5-9.5V6Z"/><path d="m9 12 2 2 4-4"/>',
    bell: '<path d="M6 8a6 6 0 0 1 12 0c0 7 3 8 3 8H3s3-1 3-8M10.3 20a1.9 1.9 0 0 0 3.4 0"/>',
    logout: '<path d="M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3M10 16l4-4-4-4M14 12H4"/>',
    admin: '<path d="M12 3 4.5 6v5.5c0 4.6 3.2 8.3 7.5 9.5 4.3-1.2 7.5-4.9 7.5-9.5V6Z"/>',
    check: '<path d="m5 12.5 4.5 4.5L19 7.5"/>',
    x: '<path d="M6 6l12 12M18 6 6 18"/>',
    copy: '<rect x="8.5" y="8.5" width="11.5" height="11.5" rx="2.5"/><path d="M15.5 8.5V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v7.5a2 2 0 0 0 2 2h2.5"/>',
    link: '<path d="M10 14a4.5 4.5 0 0 0 6.4 0l3-3a4.5 4.5 0 0 0-6.4-6.4l-1 1M14 10a4.5 4.5 0 0 0-6.4 0l-3 3a4.5 4.5 0 0 0 6.4 6.4l1-1"/>',
    external: '<path d="M14 4h6v6M20 4l-9 9M18 14v4a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4"/>',
    star: '<path d="m12 3.5 2.6 5.3 5.9.9-4.3 4.1 1 5.8-5.2-2.7-5.2 2.7 1-5.8-4.3-4.1 5.9-.9Z"/>',
    clock: '<circle cx="12" cy="12" r="8.5"/><path d="M12 7.5V12l3 2"/>',
    menu: '<path d="M4 7h16M4 12h16M4 17h16"/>',
    upload: '<path d="M12 16V5m-4.5 4.5L12 5l4.5 4.5M5 19h14"/>',
    terminal: '<rect x="3" y="4.5" width="18" height="15" rx="2.5"/><path d="m7 9.5 3 2.5-3 2.5M12.5 15H17"/>',
    rocket: '<path d="M5 15c-1.5 1.5-2 4.5-2 6 1.5 0 4.5-.5 6-2m-1.5-5.5L5 11l3-3h4.5M13 16.5 16 19l3-3v-4.5M8.5 15.5 12 12c4-4 8-8 8-8s-4 4-8 8l-3.5 3.5Z"/><circle cx="15.5" cy="8.5" r="1.3"/>',
    server: '<rect x="3.5" y="4" width="17" height="7" rx="2"/><rect x="3.5" y="13" width="17" height="7" rx="2"/><path d="M7 7.5h.01M7 16.5h.01"/>',
    sparkle: '<path d="M12 3v4m0 10v4M3 12h4m10 0h4M6 6l2.5 2.5m7 7L18 18M6 18l2.5-2.5m7-7L18 6"/>',
    heart: '<path d="M12 20s-7.5-4.5-7.5-10A4.5 4.5 0 0 1 12 7a4.5 4.5 0 0 1 7.5 3c0 5.5-7.5 10-7.5 10Z"/>',
    filter: '<path d="M4 5h16l-6 7.5V19l-4 2v-8.5Z"/>',
    eye: '<path d="M2.5 12S6 5 12 5s9.5 7 9.5 7-3.5 7-9.5 7-9.5-7-9.5-7Z"/><circle cx="12" cy="12" r="3"/>',
    edit: '<path d="M4 20h4L19 9l-4-4L4 16Z"/><path d="m13.5 6.5 4 4"/>',
    trash: '<path d="M4 7h16M9 7V4.5h6V7m-8.5 0 1 13h9l1-13"/>',
    github: '<path d="M9 19c-4.3 1.4-4.3-2.5-6-3m12 5v-3.5c0-1 .1-1.4-.5-2 2.8-.3 5.5-1.4 5.5-6a4.6 4.6 0 0 0-1.3-3.2 4.2 4.2 0 0 0-.1-3.2s-1.1-.3-3.5 1.3a12.3 12.3 0 0 0-6.2 0C6.5 2.8 5.4 3.1 5.4 3.1a4.2 4.2 0 0 0-.1 3.2A4.6 4.6 0 0 0 4 9.5c0 4.6 2.7 5.7 5.5 6-.6.6-.6 1.2-.5 2V21"/>',
    globe: '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c2.5 2.5 3.5 6 3.5 9s-1 6.5-3.5 9c-2.5-2.5-3.5-6-3.5-9s1-6.5 3.5-9Z"/>',
    bolt: '<path d="M13 3 5 13.5h6L10 21l8-10.5h-6Z"/>',
    database: '<ellipse cx="12" cy="6" rx="7.5" ry="3"/><path d="M4.5 6v12c0 1.7 3.4 3 7.5 3s7.5-1.3 7.5-3V6M4.5 12c0 1.7 3.4 3 7.5 3s7.5-1.3 7.5-3"/>',
    discord: '<path d="M8.5 7.5a13 13 0 0 1 7 0M8 17c-2 1-4 0-4 0-.5-4 .5-8 2.5-10.5L8.5 6M16 17c2 1 4 0 4 0 .5-4-.5-8-2.5-10.5L15.5 6M7.5 15.5c3 1.5 6 1.5 9 0"/><circle cx="9.5" cy="12" r="1"/><circle cx="14.5" cy="12" r="1"/>',
    tag: '<path d="M3.5 12.5V4.5a1 1 0 0 1 1-1h8l8 8a1.4 1.4 0 0 1 0 2l-7 7a1.4 1.4 0 0 1-2 0Z"/><circle cx="8" cy="8" r="1.4"/>',
    wallet: '<path d="M4 7.5A2.5 2.5 0 0 1 6.5 5H18v3"/><rect x="4" y="8" width="16.5" height="11" rx="2.5"/><path d="M16 13.5h.01"/>',
    mail: '<rect x="3" y="5" width="18" height="14" rx="2.5"/><path d="m4 7 8 6 8-6"/>',
    send: '<path d="M21 3 10 14M21 3l-7 18-4-7-7-4Z"/>',
    paperclip: '<path d="m20 11.5-8.2 8.2a5 5 0 0 1-7-7L13 4.5a3.3 3.3 0 0 1 4.7 4.7l-8.3 8.2a1.7 1.7 0 0 1-2.3-2.3l7.6-7.6"/>',
    note: '<path d="M5 4h14v11l-5 5H5Z"/><path d="M14 20v-5h5M8.5 9h7M8.5 12.5h4"/>',
    dashboard: '<rect x="3.5" y="3.5" width="7" height="9" rx="1.5"/><rect x="13.5" y="3.5" width="7" height="5" rx="1.5"/><rect x="13.5" y="11.5" width="7" height="9" rx="1.5"/><rect x="3.5" y="15.5" width="7" height="5" rx="1.5"/>',
    reply: '<path d="M9 7 4 12l5 5"/><path d="M4 12h10a6 6 0 0 1 6 6v1"/>',
    refresh: '<path d="M20 12a8 8 0 1 1-2.3-5.7L20 8.5M20 4v4.5h-4.5"/>',
    more: '<circle cx="5.5" cy="12" r="1.3"/><circle cx="12" cy="12" r="1.3"/><circle cx="18.5" cy="12" r="1.3"/>',
    history: '<path d="M3.5 12a8.5 8.5 0 1 0 2.5-6L3.5 8.5M3.5 4v4.5H8"/><path d="M12 7.5V12l3 2"/>',
    flag: '<path d="M5 21V4m0 0h11l-2 4 2 4H5"/>',
    ban: '<circle cx="12" cy="12" r="8.5"/><path d="m6 6 12 12"/>',
    megaphone: '<path d="M4 10v4a1 1 0 0 0 1 1h2l8 4V5L7 9H5a1 1 0 0 0-1 1ZM18 9.5a3.5 3.5 0 0 1 0 5M7 15l1.5 5h2.5L10 15.5"/>',
    tray: '<path d="M3 13h5l1.5 2.5h5L16 13h5M5 5h14l2 8v5a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-5Z"/>',
};

export function icon(name, extra = '') {
    return `<svg class="icon ${extra}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths[name] || paths.box}</svg>`;
}

export function esc(value) {
    return String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

export async function api(path, options = {}) {
    const headers = { ...(options.method && options.method !== 'GET' ? { 'X-CSRF-Token': auth.csrf_token } : {}), ...options.headers };
    let body = options.body;

    if (options.json !== undefined) {
        headers['Content-Type'] = 'application/json';
        body = JSON.stringify(options.json);
    }

    const response = await fetch(path, { ...options, headers, body });
    const result = await response.json().catch(() => ({}));

    if (!response.ok)
        throw new Error(result.error || 'The request could not be completed.');

    return result;
}

export function toast(message, kind = '') {
    const container = document.querySelector('#toasts');
    const item = document.createElement('div');
    item.className = 'toast ' + kind;
    item.textContent = message;
    container.append(item);
    setTimeout(() => item.remove(), 4500);
}

export function setTitle(title) {
    document.title = title ? `${title} · ${brand.name}` : brand.name;
}

// Zero-decimal currencies such as JPY are already in whole units; everything else is in cents.
export const zeroDecimal = new Set(['bif', 'clp', 'djf', 'gnf', 'jpy', 'kmf', 'krw', 'mga', 'pyg', 'rwf', 'ugx', 'vnd', 'vuv', 'xaf', 'xof', 'xpf']);

export function money(minor, currency = 'usd') {
    const units = zeroDecimal.has(currency) ? minor : minor / 100;
    return new Intl.NumberFormat(undefined, { style: 'currency', currency: currency.toUpperCase() }).format(units);
}

export const number = n => Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1 }).format(n || 0);
export const size = n => n >= 1048576 ? (n / 1048576).toFixed(1) + ' MB' : Math.max(1, Math.round(n / 1024)) + ' KB';
export const date = s => new Date(s).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
export const initials = name => String(name || '?').split(/\s+/).filter(Boolean).slice(0, 2).map(w => Array.from(w)[0]).join('').toUpperCase() || '?';

export function ago(value) {
    const seconds = (Date.now() - (typeof value === 'number' ? value * 1000 : new Date(value).getTime())) / 1000;
    const steps = [[60, 'second'], [60, 'minute'], [24, 'hour'], [30, 'day'], [12, 'month'], [Infinity, 'year']];
    let amount = seconds;

    for (const [limit, unit] of steps) {
        if (Math.abs(amount) < limit)
            return new Intl.RelativeTimeFormat('en', { numeric: 'auto' }).format(-Math.round(amount), unit);

        amount /= limit;
    }

    return '';
}

export function avatar(person, sizeClass = '') {
    const name = person?.display_name || person?.name || '?';

    if (person?.avatar)
        return `<img class="avatar ${sizeClass}" src="${esc(person.avatar)}" alt="" loading="lazy">`;

    return `<span class="avatar ${sizeClass}" aria-hidden="true">${esc(initials(name))}</span>`;
}

export function modIcon(mod) {
    if (mod.logo)
        return `<img class="mod-icon" src="${esc(mod.logo)}" alt="" loading="lazy">`;

    return `<span class="mod-icon" aria-hidden="true">${esc(initials(mod.name))}</span>`;
}

const badgeTips = {
    admin: `${brand.name} Staff`,
    verified: `This user has been verified by ${brand.name} staff as a trustable, reputable mod developer. They are who they say they are.`,
};

// Badges inside links can't be focusable, so only standalone badges get a tab stop for their tooltip.
export function userBadges(user, focusable = false) {
    const badge = (kind, glyph, label) => `<span class="user-badge ${kind}"${focusable ? ' tabindex="0"' : ''}>${icon(glyph)}${label}<span class="tip" role="tooltip">${badgeTips[kind]}</span></span>`;
    return (user.is_admin ? badge('admin', 'admin', 'Admin') : '') + (user.is_verified ? badge('verified', 'check', 'Verified') : '');
}

export function modBadges(mod) {
    return (mod.badges || []).map(badge => `<span class="forge-badge">✦ ${esc(badge)}</span>`).join('');
}

export function plainText(markdown) {
    return String(markdown || '').replace(/```[\s\S]*?```/g, ' ').replace(/[#*`>_~\[\]]/g, '').replace(/\([^)]*\)/g, '').replace(/\s+/g, ' ').trim();
}

export function modUrl(mod) {
    try {
        const url = new URL(mod.url, location.origin);
        return url.pathname + url.search;
    } catch {
        return '/view/' + encodeURIComponent(mod.id || mod.guid);
    }
}

export function modCard(mod) {
    const status = mod.status && mod.status !== 'approved'
        ? `<span class="pill ${mod.status === 'pending' ? 'warn' : 'bad'}">${mod.status === 'pending' ? 'Pending review' : esc(mod.status)}</span>`
        : `<span class="pill">${esc(mod.category)}</span>`;

    return `<a class="mod-card" href="${esc(modUrl(mod))}">
        <div class="card-top">${modIcon(mod)}${status}</div>
        <h3>${esc(mod.name)}</h3>
        <div class="by">by ${esc(mod.author.name)} ${userBadges(mod.author)}</div>
        ${mod.badges?.length ? `<div class="badges">${modBadges(mod)}</div>` : ''}
        <p class="summary">${esc(plainText(mod.description))}</p>
        <div class="card-foot"><span>${icon('download')}${number(mod.downloads)}</span><span>${icon('clock')}${esc(ago(mod.updated_at))}</span><span class="spacer"></span>${mod.gated ? `<span class="price">${esc(mod.owned && mod.author.id !== auth.user?.id ? storeCopy.cardOwned : mod.price_cents > 0 ? money(mod.price_cents, mod.currency) : storeCopy.tierFrom.replace('{price}', money(mod.min_tier_cents, mod.currency)))}</span>` : ''}<span>v${esc(mod.current_version || '—')}</span></div>
    </a>`;
}

export function skeletonGrid(count = 6) {
    return `<div class="mod-grid">${'<div class="skeleton"></div>'.repeat(count)}</div>`;
}

export function emptyState(title, text, action = '') {
    return `<div class="empty"><h2>${esc(title)}</h2><p>${esc(text)}</p>${action}</div>`;
}

export function pagination(container, total, limit, page, change) {
    const pages = Math.max(1, Math.ceil(total / limit));

    if (pages <= 1) {
        container.innerHTML = '';
        return;
    }

    container.innerHTML = `<div class="pagination"><button class="button subtle small" data-page="prev" ${page <= 1 ? 'disabled' : ''}>${icon('back')}Previous</button><span>${page} / ${pages}</span><button class="button subtle small" data-page="next" ${page >= pages ? 'disabled' : ''}>Next${icon('next')}</button></div>`;
    container.querySelector('[data-page=prev]').onclick = () => change(page - 1);
    container.querySelector('[data-page=next]').onclick = () => change(page + 1);
}

export async function copy(text, label = 'Copied to clipboard.') {
    try {
        await navigator.clipboard.writeText(text);
        toast(label);
    } catch {
        toast('Select the text and copy it manually.', 'error');
    }
}

export function bindCopy(root) {
    root.querySelectorAll('[data-copy]').forEach(button => button.onclick = event => {
        event.preventDefault();
        copy(button.dataset.copy, button.dataset.copyLabel || 'Copied to clipboard.');
    });
}

// The CSP blocks inline style attributes, so widths are applied through the DOM instead.
export function applyWidths(root) {
    root.querySelectorAll('[data-width]').forEach(el => el.style.width = Math.max(0, Math.min(100, Number(el.dataset.width))) + '%');
}


export function readAccent() {
    try {
        return localStorage.getItem('monkeforge.accent') || DEFAULT_ACCENT;
    } catch {
        return DEFAULT_ACCENT;
    }
}

export function storeAccent(color) {
    try {
        localStorage.setItem('monkeforge.accent', color);
        return true;
    } catch {
        return false;
    }
}

let faviconTemplate;

export function applyAccent(color) {
    if (!/^#[\da-f]{6}$/i.test(color))
        color = DEFAULT_ACCENT;

    const rgb = color.match(/[\da-f]{2}/gi).map(x => parseInt(x, 16));
    const linear = rgb.map(v => { v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4; });
    const luminance = linear[0] * .2126 + linear[1] * .7152 + linear[2] * .0722;
    const style = document.documentElement.style;
    const rgba = alpha => `rgb(${rgb.join(' ')} / ${alpha}%)`;

    style.setProperty('--accent', color);
    style.setProperty('--accent-ink', luminance > .4 ? '#0c0d10' : '#ffffff');
    style.setProperty('--accent-soft', rgba(14));
    style.setProperty('--accent-line', rgba(42));
    style.setProperty('--accent-glow', rgba(38));

    if (!brand.tint_favicon)
        return;

    faviconTemplate ??= fetch('/static/logo.svg').then(r => r.ok ? r.text() : null).catch(() => null);
    faviconTemplate.then(svg => {
        const link = document.querySelector('link[rel="icon"]');

        if (svg && link)
            link.href = 'data:image/svg+xml,' + encodeURIComponent(svg.replace('fill="#ffffff"', `fill="${color}"`));
    });
}

export const signInNext = ['/browse', '/upload', '/profile', '/admin', '/settings', '/setup'];

export function signInUrl(next = location.pathname) {
    return '/login?next=' + encodeURIComponent(signInNext.includes(next) ? next : '/browse');
}

export function requireSignIn(title, text) {
    const next = signInNext.includes(location.pathname) ? location.pathname : '/browse';
    return `<section class="signin-card surface"><span class="brand-mark" aria-hidden="true"></span><h1>${esc(title)}</h1><p>${esc(text)}</p>
        <div class="signin-actions"><a class="button large" href="${signInUrl()}">${icon('user')}Sign in</a>
        ${auth.email_signup ? `<a class="button subtle large" href="/signup?next=${encodeURIComponent(next)}">Create an account</a>` : ''}</div></section>`;
}

export function showError(title, message) {
    app.innerHTML = `<section class="empty"><h2>${esc(title)}</h2><p>${esc(message)}</p><a class="button" href="/browse">${icon('back')}Back to browse</a></section>`;
}

export function onOutside(element, close) {
    const handler = event => {
        if (!element.contains(event.target)) {
            close();
            document.removeEventListener('pointerdown', handler);
        }
    };

    setTimeout(() => document.addEventListener('pointerdown', handler));
    return () => document.removeEventListener('pointerdown', handler);
}

let groupsRequest;

export function myGroups() {
    if (!auth.user)
        return Promise.resolve([]);

    groupsRequest ??= api('/api/v1/groups').then(result => result.items).catch(() => []);
    return groupsRequest;
}

export async function canManageMod(author) {
    if (!auth.user)
        return false;

    if (author.id === auth.user.id)
        return true;

    return (await myGroups()).some(group => group.id === author.id);
}
