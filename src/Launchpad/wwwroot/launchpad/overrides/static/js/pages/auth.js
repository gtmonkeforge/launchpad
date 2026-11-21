import { app, auth, esc, icon, setTitle } from '../core.js';
import { call, launchpad } from '/_launchpad/bridge.js';

const params = new URLSearchParams(location.search);
const next = /^\/(?!\/)/.test(params.get('next') || '') ? params.get('next') : '/browse';

export async function render(page) {
    if (page === 'login')
        return login();

    const titles = { signup: 'Create an account', 'verify-email': 'Confirm your email', 'reset-password': 'Reset your password', unsubscribe: 'Email preferences' };
    setTitle(titles[page] || 'Account');
    const url = (launchpad.forge || 'https://monkeforge.org') + location.pathname + location.search;

    app.innerHTML = `<section class="signin-card surface"><span class="brand-mark" aria-hidden="true"></span>
        <h1>${esc(titles[page] || 'Continue on MonkeForge')}</h1>
        <p>This finishes on monkeforge.org in your browser. Come back to Launchpad and sign in when you’re done.</p>
        <div class="signin-actions"><a class="button large" data-lp-external href="${esc(url)}">${icon('external')}Open monkeforge.org</a><a class="button subtle large" href="/login">Sign in</a></div></section>`;
}

function login() {
    setTitle('Sign in');

    if (auth.user) {
        location.replace(next);
        return;
    }

    app.innerHTML = `<section class="signin-card surface"><span class="brand-mark" aria-hidden="true"></span>
        <h1>Sign in to MonkeForge</h1>
        <p>Your browser opens monkeforge.org, where you sign in with Discord, a passkey, or your password, then allow Launchpad to use your account. Launchpad picks it up from there.</p>
        <div class="signin-actions" id="signin-actions"><button class="button large" id="sign-in">${icon('user')}Sign in with your browser</button>
            <a class="button subtle large" data-lp-external href="${esc((launchpad.forge || 'https://monkeforge.org') + '/signup')}">Create an account</a></div>
        <p class="auth-status" id="signin-status" role="status"></p></section>`;

    const button = app.querySelector('#sign-in');
    const status = app.querySelector('#signin-status');

    button.onclick = async () => {
        button.disabled = true;
        status.className = 'auth-status';
        status.innerHTML = `<span class="spinner" aria-hidden="true"></span> Waiting for you to finish in your browser… <button class="button ghost small" id="cancel-sign-in">Cancel</button>`;
        status.querySelector('#cancel-sign-in').onclick = () => call('auth.cancel');

        try {
            await call('auth.signIn');
            location.replace(next);
        } catch (error) {
            status.className = 'auth-status error';
            status.textContent = error.message;
            button.disabled = false;
        }
    };
}
