import { api, applyAccent, auth, boot, readAccent, showError } from './core.js';
import { renderFooter, renderHeader } from './shell.js';

const routes = [
    [/^\/browse$/, () => import('./pages/browse.js')],
    [/^\/view\/([^/]+)\/monkeshield$/, () => import('./pages/shield.js')],
    [/^\/view\/([^/]+)\/(extensions)$/, () => import('./pages/mod.js')],
    [/^\/view\/([^/]+)\/(docs)(?:\/([^/]+))?$/, () => import('./pages/mod.js')],
    [/^\/view\/([^/]+)$/, () => import('./pages/mod.js')],
    [/^\/upload$/, () => import('./pages/publish.js')],
    [/^\/profile\/([^/]+)$/, () => import('./pages/profile.js')],
    [/^\/settings(?:\/([a-z]+))?(?:\/([a-f0-9]{24}))?$/, () => import('./pages/settings.js')],
    [/^\/oauth\/authorize$/, () => import('./pages/oauth.js')],
    [/^\/(login|signup|verify-email|reset-password|unsubscribe)$/, () => import('./pages/auth.js')],
    [/^\/(friends|chat|groups|transfers|notifications)$/, () => import('./pages/social.js')],
    [/^\/software(?:\/([a-z]+))?$/, () => import('./pages/software.js')],
    [/^\/(terms|privacy)$/, () => import('./pages/legal.js')],
    [/^\/docs(?:\/(.+))?$/, () => import('./pages/docs.js')],
];

async function start() {
    applyAccent(readAccent());

    try {
        Object.assign(auth, await api('/api/v1/session'));
    } catch {
    }

    applyAccent(auth.user?.accent_color || readAccent());
    renderHeader();
    renderFooter();

    if (boot.error) {
        showError(boot.status === 404 ? 'Page not found' : 'Something went wrong', boot.error);
        return;
    }

    const path = decodeURIComponent(location.pathname);

    for (const [pattern, load] of routes) {
        const match = path.match(pattern);

        if (!match)
            continue;

        try {
            const page = await load();
            await page.render(...match.slice(1));
        } catch (error) {
            showError('Unable to load this page', error.message);
        }

        return;
    }

    showError('Page not found', 'The requested page does not exist.');
}

start();
