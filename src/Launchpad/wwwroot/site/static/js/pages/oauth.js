import { api, app, appLogo, auth, avatar, brand, esc, icon, setTitle, verifiedMark } from '../core.js';

const fields = ['response_type', 'client_id', 'redirect_uri', 'scope', 'state', 'code_challenge', 'code_challenge_method', 'prompt'];

function card(inner) {
    return `<section class="signin-card surface consent-card">${inner}</section>`;
}

function problem(title, message, action = '') {
    app.innerHTML = card(`<span class="brand-mark" aria-hidden="true"></span><h1>${esc(title)}</h1><p>${esc(message)}</p>${action}`);
}

// /oauth/authorize: the screen where someone signs in to an app with their account and chooses whether to let it in.
export async function render() {
    setTitle('Authorize an app');
    const params = new URLSearchParams(location.search);
    const request = Object.fromEntries(fields.filter(key => params.has(key)).map(key => [key, params.get(key)]));
    let info;

    try {
        info = await api('/api/v1/oauth/authorize?' + new URLSearchParams(request));
    } catch (error) {
        problem('This sign-in link doesn’t work', error.message);
        return;
    }

    // The client and redirect are genuine but the request isn't: say why, and offer the way back to the app.
    if (info.error) {
        problem(`${info.app ? info.app.name : 'The app'} sent a request we can’t accept`, info.error, `<a class="button subtle" href="${esc(info.redirect)}">${icon('back')}Back to the app</a>`);
        return;
    }

    if (!auth.user) {
        location.replace('/login?' + new URLSearchParams({ next: location.pathname + location.search }));
        return;
    }

    const decide = async approve => {
        app.querySelectorAll('button').forEach(button => button.disabled = true);

        try {
            const result = await api('/api/v1/oauth/authorize', { method: 'POST', json: { request, approve } });
            location.assign(result.redirect);
        } catch (error) {
            const status = app.querySelector('#consent-status');

            if (status)
                status.textContent = error.message;
            else
                problem('Something went wrong', error.message);

            app.querySelectorAll('button').forEach(button => button.disabled = false);
        }
    };

    // Already allowed with these permissions: carry straight on, like signing in anywhere else with an account.
    if (info.granted) {
        problem(`Continuing to ${info.app.name}…`, 'You’ve already authorized this app.');
        await decide(true);
        return;
    }

    const owner = info.app.owner;
    app.innerHTML = card(`
        <div class="consent-logos">${appLogo(info.app, 'lg')}<span class="consent-link" aria-hidden="true">${icon('link')}</span><span class="brand-mark" aria-hidden="true"></span></div>
        <h1 class="consent-title"><span>${esc(info.app.name)}</span>${info.app.verified ? verifiedMark() : ''}</h1>
        <p>wants to access your ${esc(brand.name)} account</p>
        ${owner ? `<p class="hint">Made by <a href="${esc(owner.profile_url)}" target="_blank" rel="noopener">${esc(owner.name)}</a>${info.app.homepage_url ? ` · <a href="${esc(info.app.homepage_url)}" target="_blank" rel="noopener noreferrer">${esc(new URL(info.app.homepage_url).host)}</a>` : ''}</p>` : ''}
        ${info.app.verified ? '' : `<p class="notice consent-warning">${esc(brand.name)} staff haven’t verified this app. Only continue if you trust whoever sent you here.</p>`}
        <div class="consent-account">${avatar(auth.user, 'sm')}<span>Signed in as <strong>${esc(auth.user.display_name)}</strong></span><span class="spacer"></span><a class="hint" href="#" id="switch-account">Not you?</a></div>
        <h2 class="consent-heading">This will allow ${esc(info.app.name)} to:</h2>
        <ul class="consent-scopes">${info.scopes.map(scope => `<li>${icon(scope.key === 'account' ? 'shield' : scope.key === 'email' ? 'mail' : scope.key === 'publish' ? 'upload' : 'user')}<span>${esc(scope.label)}</span></li>`).join('')}</ul>
        <p class="hint consent-foot">It can’t see your password or change how you sign in. You’ll be sent to <strong>${esc(info.redirect_host)}</strong>. You can remove its access any time in <a href="/settings/security#authorized-apps">Sign-in &amp; security</a>.</p>
        <p class="auth-status error" id="consent-status" role="status" aria-live="polite"></p>
        <div class="consent-actions"><button class="button ghost large" id="consent-deny">Cancel</button><button class="button large" id="consent-allow">Authorize</button></div>`);

    app.querySelector('#consent-allow').onclick = () => decide(true);
    app.querySelector('#consent-deny').onclick = () => decide(false);
    app.querySelector('#switch-account').onclick = async event => {
        event.preventDefault();
        await api('/api/v1/logout', { method: 'POST' }).catch(() => {});
        location.assign('/login?' + new URLSearchParams({ next: location.pathname + location.search }));
    };
}
