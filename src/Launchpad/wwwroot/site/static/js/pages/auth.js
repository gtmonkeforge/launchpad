import { api, app, appLogo, auth, brand, esc, icon, safeNext, setTitle, toast, verifiedMark } from '../core.js';
import { cancelled, passkeysSupported, signInWithPasskey } from '../passkeys.js';

const params = new URLSearchParams(location.search);
const next = safeNext(params.get('next'));

// Signing in on the way to an app's authorization screen: name the app, with its verified checkmark, above the form.
const authorizing = next.startsWith('/oauth/authorize?');

function carried(extra = {}) {
    const query = new URLSearchParams({ next, ...extra });
    return query.toString();
}

async function showApp(root) {
    const slot = root.querySelector('#auth-app');

    if (!slot)
        return;

    try {
        const info = await api('/api/v1/oauth/authorize' + next.slice('/oauth/authorize'.length));

        if (info.app)
            slot.innerHTML = `<p class="notice auth-app">${appLogo(info.app, 'sm')}<span>Sign in to continue to <strong>${esc(info.app.name)}</strong>${info.app.verified ? verifiedMark() : ''}</span></p>`;
    } catch {
    }
}

function card(title, inner) {
    return `<section class="signin-card surface auth-card"><span class="brand-mark" aria-hidden="true"></span><h1>${esc(title)}</h1>${inner}</section>`;
}

// Sign in and Create account share one card, switched by these tabs.
function tabs(active) {
    if (!auth.email_signup)
        return '';

    const tab = (key, href, label) => `<a role="tab" href="${href}" aria-selected="${key === active}" ${key === active ? 'aria-current="page"' : ''}>${label}</a>`;
    return `<nav class="auth-tabs" role="tablist" aria-label="Account">${tab('login', '/login?' + esc(carried()), 'Sign in')}${tab('signup', '/signup?' + esc(carried()), 'Create account')}</nav>`;
}

const discordButton = label => auth.oauth_configured ? `<a class="button discord large block" href="/auth/discord?${esc(carried())}">${icon('discord')}${label}</a>` : '';

function busy(form, on) {
    form.querySelectorAll('button, input').forEach(el => el.disabled = on);
}

function status(root, message, kind = 'error') {
    const el = root.querySelector('#auth-status');
    el.className = 'auth-status ' + kind;
    el.textContent = message;
}

const passwordField = (id, label, autocomplete, hint = '') => `<div class="field"><label for="${id}">${esc(label)}${hint ? `<span>${esc(hint)}</span>` : ''}</label>
    <div class="password-input"><input id="${id}" name="${id}" type="password" required maxlength="128" autocomplete="${autocomplete}"><button type="button" class="button ghost small" data-reveal="${id}" aria-label="Show password">${icon('eye')}</button></div></div>`;

function bindReveal(root) {
    root.querySelectorAll('[data-reveal]').forEach(button => button.onclick = () => {
        const input = root.querySelector('#' + button.dataset.reveal);
        const show = input.type === 'password';
        input.type = show ? 'text' : 'password';
        button.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
    });
}

export async function render(page) {
    if (page === 'login')
        return login();

    if (page === 'signup')
        return signup();

    if (page === 'verify-email')
        return verify();

    if (page === 'reset-password')
        return params.get('token') ? resetForm(params.get('token')) : forgot();

    return unsubscribe();
}

function login() {
    setTitle('Sign in');

    if (auth.user) {
        location.replace(next);
        return;
    }

    const discord = discordButton('Sign in with Discord');
    const passkey = passkeysSupported() ? `<button type="button" class="button subtle large block" id="passkey-login">${icon('key')}Sign in with a passkey</button>` : '';
    const password = auth.mail_configured ? `<form id="login-form" class="auth-form" novalidate>
            <div class="field"><label for="login">Username or email</label><input id="login" name="login" required maxlength="254" autocomplete="username" autocapitalize="none" spellcheck="false"></div>
            ${passwordField('password', 'Password', 'current-password')}
            <div class="auth-forgot"><a class="hint" href="/reset-password">Forgot your password?</a></div>
            <button class="button large block" type="submit">Sign in</button>
        </form>` : '';

    // The password form leads; Discord and passkeys are the alternatives underneath it.
    app.innerHTML = card(`Sign in to ${brand.name}`, `
        ${tabs('login')}
        ${authorizing ? '<div id="auth-app"><p class="notice">Sign in to continue to an app. You’ll choose what it can access next.</p></div>' : ''}
        ${params.get('verified') ? '<p class="notice">Your email is confirmed. Sign in to continue.</p>' : ''}
        ${password}
        <p class="auth-status" id="auth-status" role="status" aria-live="polite"></p>
        ${password && (discord || passkey) ? '<div class="auth-divider"><span>or</span></div>' : ''}
        <div class="auth-methods">${discord}${passkey}</div>
        ${!discord && !password ? '<p class="notice">Sign-in isn’t set up on this server yet. The site owner needs to add Discord or email settings.</p>' : ''}`);

    bindReveal(app);
    showApp(app);
    const done = result => location.assign(result.redirect || next);

    app.querySelector('#passkey-login')?.addEventListener('click', async event => {
        const button = event.currentTarget;
        button.disabled = true;
        status(app, '', '');

        try {
            done(await signInWithPasskey({ next }));
        } catch (error) {
            if (!cancelled(error))
                status(app, error.message);

            button.disabled = false;
        }
    });

    const form = app.querySelector('#login-form');

    if (!form)
        return;

    form.querySelector('#login').focus();
    form.onsubmit = async event => {
        event.preventDefault();

        if (!form.reportValidity())
            return;

        busy(form, true);
        status(app, '', '');

        try {
            done(await api('/api/v1/auth/login', { method: 'POST', json: { login: form.login.value.trim(), password: form.password.value, next } }));
        } catch (error) {
            status(app, error.message);
            busy(form, false);
            form.password.select();
        }
    };
}

function signup() {
    setTitle('Create an account');

    if (auth.user) {
        location.replace(next);
        return;
    }

    if (!auth.email_signup) {
        app.innerHTML = card('Create an account', `<p>New accounts on ${esc(brand.name)} are made by signing in with Discord.</p>
            ${discordButton('Sign up with Discord')}`);
        return;
    }

    app.innerHTML = card(`Join ${brand.name}`, `
        ${tabs('signup')}
        <form id="signup-form" class="auth-form" novalidate>
            <div class="field"><label for="username">Username<span>Shown on your profile and mods</span></label><input id="username" name="username" required minlength="3" maxlength="32" pattern="[A-Za-z0-9][A-Za-z0-9_.\\-]{2,31}" autocomplete="username" autocapitalize="none" spellcheck="false"></div>
            <div class="field"><label for="email">Email<span>Never shown publicly</span></label><input id="email" name="email" type="email" required maxlength="254" autocomplete="email" spellcheck="false"></div>
            ${passwordField('password', 'Password', 'new-password', 'At least 8 characters')}
            <p class="hint">By creating an account you agree to the <a href="/terms">Terms</a> and <a href="/privacy">Privacy Policy</a>.</p>
            <button class="button large block" type="submit">Create account</button>
        </form>
        <p class="auth-status" id="auth-status" role="status" aria-live="polite"></p>
        ${auth.oauth_configured ? `<div class="auth-divider"><span>or</span></div><div class="auth-methods">${discordButton('Sign up with Discord')}</div>` : ''}`);

    bindReveal(app);
    const form = app.querySelector('#signup-form');
    form.username.focus();
    form.onsubmit = async event => {
        event.preventDefault();

        if (!form.reportValidity())
            return;

        busy(form, true);
        status(app, '', '');

        try {
            const result = await api('/api/v1/auth/signup', { method: 'POST', json: { username: form.username.value.trim(), email: form.email.value.trim(), password: form.password.value } });
            checkInbox(result.email);
        } catch (error) {
            status(app, error.message);
            busy(form, false);
        }
    };
}

function checkInbox(email) {
    app.innerHTML = card('Check your inbox', `
        <p>We sent a link to <strong>${esc(email)}</strong>. Open it to confirm your address and finish signing up. It works for 24 hours.</p>
        <p class="hint">Nothing there? Check your spam folder, or send it again.</p>
        <button class="button subtle" id="resend">Send the link again</button>
        <p class="auth-status" id="auth-status" role="status" aria-live="polite"></p>
        <p class="hint auth-foot"><a href="/login?${esc(carried())}">Back to sign in</a></p>`);

    app.querySelector('#resend').onclick = async event => {
        event.currentTarget.disabled = true;

        try {
            await api('/api/v1/auth/resend', { method: 'POST', json: { email } });
            status(app, 'Sent. It can take a minute to arrive.', 'good');
        } catch (error) {
            status(app, error.message);
            event.currentTarget.disabled = false;
        }
    };
}

// The link only shows a button: mail scanners that follow links won't press it, so the one-time token survives until the person does.
function verify() {
    setTitle('Confirm your email');
    const token = params.get('token') || '';
    app.innerHTML = card('Confirm your email', `
        <p>Press the button to confirm this address${token ? '' : '. This link is missing its code; open the link from the email again'}.</p>
        <button class="button large block" id="confirm" ${token ? '' : 'disabled'}>${icon('check')}Confirm email</button>
        <p class="auth-status" id="auth-status" role="status" aria-live="polite"></p>`);

    app.querySelector('#confirm').onclick = async event => {
        const button = event.currentTarget;
        button.disabled = true;

        try {
            const result = await api('/api/v1/auth/verify', { method: 'POST', json: { token, next: '/browse' } });
            history.replaceState(null, '', '/verify-email');

            if (result.changed) {
                app.innerHTML = card('Email updated', `<p>Your account now uses <strong>${esc(result.email)}</strong>.</p><a class="button" href="/settings/security">Back to settings</a>`);
                return;
            }

            toast(`Welcome to ${brand.name}!`);
            location.assign(result.redirect || '/browse');
        } catch (error) {
            status(app, error.message);
        }
    };
}

function forgot() {
    setTitle('Reset your password');
    app.innerHTML = card('Reset your password', `
        <p>Enter the email address on your account and we’ll send you a link to choose a new password.</p>
        <form id="forgot-form" class="auth-form" novalidate>
            <div class="field"><label for="email">Email</label><input id="email" name="email" type="email" required maxlength="254" autocomplete="email" spellcheck="false"></div>
            <button class="button large block" type="submit">Send reset link</button>
        </form>
        <p class="auth-status" id="auth-status" role="status" aria-live="polite"></p>
        <p class="hint auth-foot"><a href="/login">Back to sign in</a></p>`);

    const form = app.querySelector('#forgot-form');
    form.email.focus();
    form.onsubmit = async event => {
        event.preventDefault();

        if (!form.reportValidity())
            return;

        busy(form, true);

        try {
            await api('/api/v1/auth/forgot', { method: 'POST', json: { email: form.email.value.trim() } });
            app.innerHTML = card('Check your inbox', `<p>If an account uses <strong>${esc(form.email.value.trim())}</strong>, we’ve sent it a reset link. It works for one hour.</p><p class="hint">Accounts that sign in with Discord don’t have a password; we’ll tell you if that’s the case.</p><p class="hint auth-foot"><a href="/login">Back to sign in</a></p>`);
        } catch (error) {
            status(app, error.message);
            busy(form, false);
        }
    };
}

function resetForm(token) {
    setTitle('Choose a new password');
    app.innerHTML = card('Choose a new password', `
        <form id="reset-form" class="auth-form" novalidate>
            ${passwordField('password', 'New password', 'new-password', 'At least 8 characters')}
            ${passwordField('confirm', 'Type it again', 'new-password')}
            <button class="button large block" type="submit">Save password</button>
        </form>
        <p class="hint">Saving signs you out on every other device.</p>
        <p class="auth-status" id="auth-status" role="status" aria-live="polite"></p>`);

    bindReveal(app);
    const form = app.querySelector('#reset-form');
    form.password.focus();
    form.onsubmit = async event => {
        event.preventDefault();

        if (!form.reportValidity())
            return;

        if (form.password.value !== form.confirm.value) {
            status(app, 'The two passwords don’t match.');
            return;
        }

        busy(form, true);

        try {
            const result = await api('/api/v1/auth/reset', { method: 'POST', json: { token, password: form.password.value, next: '/browse' } });
            history.replaceState(null, '', '/reset-password');
            toast('Password changed.');
            location.assign(result.redirect || '/browse');
        } catch (error) {
            status(app, error.message);
            busy(form, false);
        }
    };
}

function unsubscribe() {
    setTitle('Unsubscribe');
    const token = params.get('token') || '';
    const topics = { releases: 'release notices', announcements: 'announcements' };
    app.innerHTML = card('Unsubscribe', `
        <p>Stop getting this kind of email from ${esc(brand.name)}? Account emails, like password resets, still arrive.</p>
        <button class="button large block" id="unsubscribe" ${token ? '' : 'disabled'}>Unsubscribe</button>
        <p class="auth-status" id="auth-status" role="status" aria-live="polite"></p>
        <p class="hint auth-foot"><a href="/settings/emails">Email settings</a></p>`);

    app.querySelector('#unsubscribe').onclick = async event => {
        event.currentTarget.disabled = true;

        try {
            const result = await api('/api/v1/email/unsubscribe?token=' + encodeURIComponent(token), { method: 'POST' });
            app.innerHTML = card('You’re unsubscribed', `<p>You won’t get ${esc(topics[result.topic] || 'these emails')} any more. You can turn them back on in your email settings.</p><a class="button subtle" href="/settings/emails">Email settings</a>`);
        } catch (error) {
            status(app, error.message);
        }
    };
}
