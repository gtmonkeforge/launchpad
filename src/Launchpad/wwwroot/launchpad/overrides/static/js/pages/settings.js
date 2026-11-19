import { render as siteRender } from './settings.site.js';
import { app, esc, icon } from '../core.js';
import { launchpad } from '/_launchpad/bridge.js';

export async function render(section) {
    await siteRender(section);

    app.querySelector('.side-nav')?.insertAdjacentHTML('beforeend', `<a href="/launchpad">${icon('game')}Game & mod loader</a>`);

    for (const notice of app.querySelectorAll('.notice')) {
        if (/passkeys/i.test(notice.textContent)) {
            const forge = launchpad.forge || 'https://monkeforge.org';
            notice.innerHTML = `Passkeys are tied to monkeforge.org, so add them there. You can still rename and remove them here. <a data-lp-external href="${esc(forge)}/settings/security">Open security settings</a>`;
        }
    }
}
