import { auth, toast } from '/static/js/core.js';
import { call, installDllFiles, launchpad, on, send } from './bridge.js';

const forge = launchpad.forge || 'https://monkeforge.org';
const windows = launchpad.os === 'windows';

const appRoute = /^\/(?:browse|library|upload|terms|privacy|login|signup|verify-email|reset-password|unsubscribe|friends|chat|groups|transfers|notifications|software(?:\/[a-z]+)?|profile(?:\/[^/]+)?|settings(?:\/[a-z]+(?:\/[a-f0-9]{24})?)?|launchpad(?:\/[a-z]+)?|docs(?:\/.*)?|view\/[^/]+(?:\/monkeshield|\/extensions|\/docs(?:\/[^/]+)?)?)?\/?$/;

export function isAppUrl(url) {
    return url.origin === location.origin && appRoute.test(url.pathname);
}

let returningFromBrowser = false;

export function openExternal(href) {
    let url;

    try {
        url = new URL(href, location.href);
    } catch {
        return;
    }

    if (url.origin === location.origin)
        url = new URL(url.pathname + url.search + url.hash, forge);

    if (url.protocol !== 'https:' && url.protocol !== 'http:')
        return;

    returningFromBrowser = true;
    call('shell.open', { url: url.href }).catch(error => toast(error.message, 'error'));
}

function go(href, replace = false) {
    let url;

    try {
        url = new URL(href, location.href);
    } catch {
        return;
    }

    if (url.origin === forge && appRoute.test(url.pathname))
        url = new URL(url.pathname + url.search + url.hash, location.origin);

    if (isAppUrl(url)) {
        // Pages switch in place when the router can; otherwise fall back to a full load.
        if (!window.__lpRoute?.(url, replace))
            replace ? location.replace(url.href) : location.assign(url.href);
    } else
        openExternal(url.href);
}

window.__lpNavigate = href => go(href);
window.__lpNavigate.replace = href => go(href, true);
window.open = href => {
    if (href)
        go(href);

    return null;
};

window.navigation?.addEventListener('navigate', event => {
    const url = new URL(event.destination.url);

    if (event.cancelable && !event.hashChange && !isAppUrl(url) && !url.href.startsWith('blob:')) {
        event.preventDefault();
        openExternal(url.href);
    }
});

window.addEventListener('focus', () => {
    if (returningFromBrowser) {
        returningFromBrowser = false;

        // Something may have changed on the website (a purchase, a sign-in); refresh the page in place.
        if (!window.__lpRefresh?.())
            location.reload();
    }
});

if (navigator.clipboard?.writeText) {
    const write = navigator.clipboard.writeText.bind(navigator.clipboard);
    navigator.clipboard.writeText = text => write(String(text).split(location.origin).join(forge));
}

function park(link) {
    if (link?.hasAttribute('href') && !link.hasAttribute('download')) {
        link.dataset.lpHref = link.getAttribute('href');
        link.removeAttribute('href');

        if (!link.hasAttribute('tabindex'))
            link.dataset.lpTabindex = link.tabIndex = 0;
    }
}

function unpark(link) {
    if (link?.dataset.lpHref !== undefined && !link.matches(':hover') && document.activeElement !== link) {
        link.setAttribute('href', link.dataset.lpHref);
        delete link.dataset.lpHref;

        if (link.dataset.lpTabindex !== undefined) {
            link.removeAttribute('tabindex');
            delete link.dataset.lpTabindex;
        }
    }
}

document.addEventListener('mouseover', event => park(event.target.closest?.('a[href]')), true);
document.addEventListener('focusin', event => park(event.target.closest?.('a[href]')), true);
document.addEventListener('mouseout', event => {
    const link = event.target.closest?.('a[data-lp-href]');

    if (link && !link.contains(event.relatedTarget))
        setTimeout(() => unpark(link));
}, true);
document.addEventListener('focusout', event => setTimeout(() => unpark(event.target.closest?.('a[data-lp-href]'))), true);

document.addEventListener('keydown', event => {
    const link = event.target.closest?.('a[data-lp-href]');

    if (link && event.key === 'Enter') {
        event.preventDefault();
        link.click();
    }
});

document.addEventListener('click', event => {
    const link = event.target.closest?.('a');

    if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || link.hasAttribute('download'))
        return;

    const href = link.dataset.lpHref ?? link.getAttribute('href');

    if (!href)
        return;

    if (href.startsWith('#')) {
        event.preventDefault();

        if (href.length > 1)
            location.hash = href;

        return;
    }

    event.preventDefault();

    if (link.hasAttribute('data-lp-external'))
        openExternal(href);
    else
        go(href);
});

const scrollWindow = window.scrollTo.bind(window);
window.scrollTo = (...args) => document.body ? document.body.scrollTo(...args) : scrollWindow(...args);

const bar = document.querySelector('#lp-titlebar');

function setMaximized(maximized) {
    document.documentElement.classList.toggle('lp-maximized', !!maximized);
    const button = bar?.querySelector('#lp-maximize');

    if (button) {
        button.setAttribute('aria-label', maximized ? 'Restore' : 'Maximize');
        button.title = maximized ? 'Restore' : 'Maximize';
    }
}

function track(down, report) {
    const target = down.currentTarget;
    let x = down.screenX, y = down.screenY, dx = 0, dy = 0, frame = 0;
    target.setPointerCapture(down.pointerId);

    const flush = () => {
        frame = 0;

        if (dx || dy)
            report(Math.round(dx), Math.round(dy));

        dx = dy = 0;
    };

    const move = event => {
        dx += event.screenX - x;
        dy += event.screenY - y;
        x = event.screenX;
        y = event.screenY;
        frame ||= requestAnimationFrame(flush);
    };

    const up = () => {
        target.removeEventListener('pointermove', move);
        target.removeEventListener('pointerup', up);
        target.removeEventListener('pointercancel', up);
        flush();
    };

    target.addEventListener('pointermove', move);
    target.addEventListener('pointerup', up);
    target.addEventListener('pointercancel', up);
}

if (bar) {
    setMaximized(launchpad.maximized);
    bar.querySelector('#lp-back').onclick = () => history.back();
    bar.querySelector('#lp-forward').onclick = () => history.forward();
    bar.querySelector('#lp-reload').onclick = () => location.reload();
    bar.querySelector('#lp-minimize').onclick = () => send('window.minimize');
    bar.querySelector('#lp-maximize').onclick = () => send('window.toggleMaximize');
    bar.querySelector('#lp-close').onclick = () => send('window.close');

    const drag = bar.querySelector('[data-drag]');
    let lastDown = 0, lastX = 0, lastY = 0;

    drag.addEventListener('pointerdown', event => {
        if (event.button !== 0)
            return;

        const now = Date.now();

        if (now - lastDown < 450 && Math.abs(event.screenX - lastX) < 6 && Math.abs(event.screenY - lastY) < 6) {
            lastDown = 0;
            send('window.toggleMaximize');
            return;
        }

        lastDown = now;
        lastX = event.screenX;
        lastY = event.screenY;

        // Windows restores a maximized window when it's dragged, like a native title bar.
        if (windows)
            send('window.drag');
        else if (!document.documentElement.classList.contains('lp-maximized'))
            track(event, (dx, dy) => send('window.moveBy', { dx, dy }));
    });

    document.querySelectorAll('.lp-resize [data-edge]').forEach(edge => edge.addEventListener('pointerdown', event => {
        if (event.button !== 0)
            return;

        if (windows)
            send('window.resize', { edge: edge.dataset.edge });
        else
            track(event, (dx, dy) => send('window.resizeBy', { edge: edge.dataset.edge, dx, dy }));
    }));
}

let activityTimer;

export function showActivity(message) {
    const box = document.querySelector('#lp-activity');

    if (!box)
        return;

    clearTimeout(activityTimer);
    box.hidden = !message;
    box.querySelector('#lp-activity-text').textContent = message || '';

    if (message)
        activityTimer = setTimeout(() => box.hidden = true, 20000);
}

const carriesFiles = event => [...(event.dataTransfer?.types || [])].includes('Files');
const pageOwnsDrops = () => !!document.querySelector('[data-drop], [data-lp-own-drop]');
let dragDepth = 0;

function setDropping(active) {
    let overlay = document.querySelector('#lp-drop');

    if (active && !overlay) {
        overlay = document.createElement('div');
        overlay.id = 'lp-drop';
        overlay.className = 'lp-drop';
        overlay.innerHTML = '<div><strong>Drop a DLL to install it</strong><span>It goes into your current mod loader’s mods folder.</span></div>';
        document.body.append(overlay);
    }

    if (overlay)
        overlay.hidden = !active;
}

window.addEventListener('dragenter', event => {
    if (!carriesFiles(event))
        return;

    event.preventDefault();
    dragDepth++;
    setDropping(!pageOwnsDrops());
});

window.addEventListener('dragover', event => {
    if (!carriesFiles(event))
        return;

    event.preventDefault();
    event.dataTransfer.dropEffect = 'copy';
});

window.addEventListener('dragleave', event => {
    if (!carriesFiles(event))
        return;

    dragDepth = Math.max(0, dragDepth - 1);

    if (!dragDepth)
        setDropping(false);
});

window.addEventListener('drop', event => {
    if (!carriesFiles(event))
        return;

    const handled = event.defaultPrevented;
    event.preventDefault();
    dragDepth = 0;
    setDropping(false);

    if (handled || pageOwnsDrops())
        return;

    installDllFiles(event.dataTransfer.files, toast);
});

// A downloaded update shows as a button in the title bar. Installs that can't update themselves (a dev build or a
// tarball unpacked somewhere else) link to the download page instead.
function showUpdate(update) {
    const button = document.querySelector('#lp-update');

    if (!button || !update)
        return;

    const ready = update.state === 'ready';
    const manual = update.state === 'available' && !update.can_install;
    button.hidden = !ready && !manual;
    button.disabled = false;
    button.querySelector('#lp-update-text').textContent = ready ? `Restart to update` : `Launchpad ${update.version} is out`;
    button.title = ready ? `Launchpad ${update.version} is downloaded. Restart to install it.` : 'Download the new version from MonkeForge';
    button.onclick = () => {
        if (!ready) {
            openExternal(forge + '/software/launchpad');
            return;
        }

        button.disabled = true;
        call('update.install').catch(error => {
            button.disabled = false;
            toast(error.message, 'error');
        });
    };

    window.dispatchEvent(new CustomEvent('launchpad:update', { detail: update }));
}

on('update', showUpdate);
call('update.state').then(showUpdate).catch(() => {});

on('window', data => setMaximized(data?.maximized));
on('progress', data => showActivity(data?.message || ''));
on('injector', data => toast(data.message, data.warning ? 'error' : ''));
on('mods', () => window.dispatchEvent(new Event('launchpad:mods')));

on('session', data => {
    if ((data?.user?.id || null) !== (auth.user?.id || null))
        location.reload();
});
