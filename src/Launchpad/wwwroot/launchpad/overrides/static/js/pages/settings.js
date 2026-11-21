import { render as siteRender } from './settings.site.js';
import { app, auth, esc, icon } from '../core.js';
import { launchpad } from '/_launchpad/bridge.js';

// Launchpad's own settings sit in the same side nav as the account settings, so every settings page has
// the same layout and nothing appears twice.
export const launchpadSections = [
    ['game', 'Game folder', 'game'],
    ['loader', 'Mod loader', 'layers'],
    ['about', 'About Launchpad', 'rocket'],
];

function decorate(current) {
    const nav = app.querySelector('.side-nav');

    if (!nav)
        return;

    // Purchases live on your profile; link there directly instead of bouncing through settings.
    nav.querySelector('a[href="/settings/purchases"]')?.setAttribute('href', auth.user ? auth.user.profile_url + '#purchases' : '/login?next=%2Fsettings');

    if (current)
        nav.querySelectorAll('[aria-current]').forEach(link => link.removeAttribute('aria-current'));

    nav.insertAdjacentHTML('beforeend', `<div class="lp-nav-label">Launchpad</div>${launchpadSections.map(([key, label, glyph]) =>
        `<a href="/launchpad/${key}" ${key === current ? 'aria-current="page"' : ''}>${icon(glyph)}${label}</a>`).join('')}`);
}

// Launchpad signs in as an OAuth app, and MonkeForge deliberately keeps app tokens away from these: they change how
// you sign in, create credentials or move money. They open on monkeforge.org instead of failing here.
const webOnly = {
    keys: 'Access keys let scripts publish as you, so they’re made on monkeforge.org.',
    payouts: 'Payouts are managed on monkeforge.org.',
    security: 'Your email, password, passkeys and authorized apps (Launchpad included) are managed on monkeforge.org.',
    emails: 'Email preferences are managed on monkeforge.org.',
    applications: 'OAuth applications you’ve registered are managed on monkeforge.org.',
};

export async function render(section, item) {
    if (webOnly[section])
        return renderWebOnly(section, item);

    await siteRender(section, item);
    decorate(null);
}

async function renderWebOnly(section, item) {
    await siteRender('appearance');
    decorate(null);

    const link = app.querySelector(`.side-nav a[href="/settings/${section}"]`);
    app.querySelectorAll('.side-nav [aria-current]').forEach(el => el.removeAttribute('aria-current'));
    link?.setAttribute('aria-current', 'page');

    const heading = app.querySelector('.page-head h1');

    if (heading && link)
        heading.textContent = link.textContent.trim();

    const forge = launchpad.forge || 'https://monkeforge.org';
    const url = `${forge}/settings/${section}${item ? '/' + encodeURIComponent(item) : ''}`;
    app.querySelector('#settings-body').innerHTML = `<section class="surface panel"><h2>Continue on monkeforge.org</h2>
        <p class="hint">${esc(webOnly[section])}</p>
        <a class="button" data-lp-external href="${esc(url)}">${icon('external')}Open in your browser</a></section>`;
}

// Draws the shared settings layout for a Launchpad section and returns the empty body to fill.
export async function renderShell(section) {
    await siteRender('appearance');
    decorate(section);

    const heading = app.querySelector('.page-head h1');

    if (heading)
        heading.textContent = launchpadSections.find(([key]) => key === section)?.[1] || 'Launchpad';

    const body = app.querySelector('#settings-body');
    body.replaceChildren();
    return body;
}
