import { DEFAULT_ACCENT, ago, api, app, applyAccent, auth, avatar, brand, date, esc, icon, money, myGroups, readAccent, requireSignIn, setTitle, signInUrl, storeAccent, toast } from '../core.js';
import { copy, fill } from '../store-copy.js';
import { cancelled, passkeysSupported, registerPasskey } from '../passkeys.js';

const sections = [
    ['appearance', 'Appearance', 'palette'],
    ['keys', 'Access keys', 'key'],
    ['channels', 'Release channels', 'lock'],
    ['purchases', copy.purchasesNav, 'tray'],
    ['payouts', copy.payoutsNav, 'wallet'],
    ['devices', copy.devicesNav, 'server'],
    ['account', 'Account', 'user'],
    ['security', 'Sign-in & security', 'lock'],
    ['emails', 'Email', 'bell'],
];

const swatches = ['#dc5c3b', '#f59e0b', '#22c55e', '#14b8a6', '#60cdff', '#6366f1', '#a855f7', '#ec4899', '#e5e7eb'];

export async function render(section = 'appearance') {
    if (!sections.some(([key]) => key === section))
        section = 'appearance';

    setTitle('Settings');
    app.innerHTML = `
        <div class="page-head"><div><h1>${esc(sections.find(([key]) => key === section)[1])}</h1></div></div>
        <div class="settings-layout">
            <nav class="side-nav surface" aria-label="Settings">${sections.map(([key, label, glyph]) => `<a href="/settings${key === 'appearance' ? '' : '/' + key}" ${key === section ? 'aria-current="page"' : ''}>${icon(glyph)}${label}</a>`).join('')}</nav>
            <div id="settings-body"></div>
        </div>`;

    const body = app.querySelector('#settings-body');

    if (section === 'appearance')
        appearance(body);
    else if (section === 'keys')
        await keys(body);
    else if (section === 'channels')
        channels(body);
    else if (section === 'purchases')
        location.replace(auth.user ? auth.user.profile_url + '#purchases' : '/browse');
    else if (section === 'payouts')
        await payouts(body);
    else if (section === 'devices')
        await devices(body);
    else if (section === 'security')
        await security(body);
    else if (section === 'emails')
        await emails(body);
    else
        account(body);
}

function appearance(body) {
    const saved = auth.user?.accent_color || readAccent();

    body.innerHTML = `<section class="surface panel">
        <h2>Accent color</h2>
        <p class="hint">Used for buttons, links, highlights and the background glow.</p>
        <div class="swatches" role="group" aria-label="Preset colors">${swatches.map(c => `<button type="button" class="swatch" data-swatch="${c}" aria-label="${c}" aria-pressed="${c === saved}"></button>`).join('')}</div>
        <div class="gap"></div>
        <div class="color-input"><input type="color" id="accent-picker" value="${esc(saved)}" aria-label="Custom color"><input type="text" id="accent-hex" class="mono-input" value="${esc(saved)}" maxlength="7" aria-label="Hex code"><button class="button ghost small" id="accent-reset">Reset</button></div>
        <div class="gap"></div>
        <div class="row"><button class="button">Primary</button><span class="pill accent">Selected</span><a class="hint" href="#" id="sample-link">A link</a></div>
        <div class="row sticky-actions"><span class="hint">${auth.user ? 'Saved to your account and used on every device.' : 'Saved in this browser. Sign in to sync it.'}</span><span class="spacer"></span><button class="button ghost" id="accent-cancel">Cancel</button><button class="button" id="accent-save">Save</button></div>
    </section>`;

    body.querySelectorAll('[data-swatch]').forEach(button => button.style.background = button.dataset.swatch);

    const picker = body.querySelector('#accent-picker');
    const hex = body.querySelector('#accent-hex');
    const set = color => {
        picker.value = hex.value = color;
        applyAccent(color);
        body.querySelectorAll('[data-swatch]').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.swatch === color)));
    };

    body.querySelectorAll('[data-swatch]').forEach(b => b.onclick = () => set(b.dataset.swatch));
    picker.oninput = () => set(picker.value);
    hex.oninput = () => {
        if (/^#[\da-f]{6}$/i.test(hex.value))
            set(hex.value.toLowerCase());
    };
    body.querySelector('#accent-reset').onclick = () => set(DEFAULT_ACCENT);
    body.querySelector('#accent-cancel').onclick = () => set(auth.user?.accent_color || readAccent());
    body.querySelector('#sample-link').onclick = event => event.preventDefault();
    body.querySelector('#accent-save').onclick = async () => {
        const color = hex.value.toLowerCase();

        try {
            if (auth.user) {
                await api('/api/v1/settings', { method: 'PUT', json: { accent_color: color } });
                auth.user.accent_color = color;
            }

            if (!storeAccent(color) && !auth.user)
                throw new Error('Browser storage is unavailable. Sign in to save settings to your account.');

            toast('Accent saved.');
        } catch (error) {
            toast(error.message, 'error');
        }
    };
}

async function keys(body) {
    if (!auth.user) {
        body.innerHTML = requireSignIn('Access keys', 'Sign in to create keys for publishing from scripts and GitHub Actions.');
        return;
    }

    body.innerHTML = `<section class="surface panel">
            <h2>Create an access key</h2>
            <p class="hint">Keys can publish releases to your mods from scripts or CI. A key is shown once and can’t be retrieved later.</p>
            <form class="row" id="key-form"><label class="sr-only" for="key-name">Key name</label><input id="key-name" required maxlength="80" placeholder="GitHub Actions"><button class="button" type="submit">${icon('plus')}Create key</button></form>
            <div class="new-key" id="new-key" hidden><p><strong>Copy this key now.</strong> Store it in your repository secrets; it won’t be shown again.</p><div class="copy-field"><input id="key-token" readonly aria-label="New key" spellcheck="false"><button class="button subtle" id="key-copy">${icon('copy')}Copy</button></div><p><button class="button ghost small" id="key-done">I saved it</button></p></div>
        </section>
        <section class="surface panel"><h2>Your keys</h2><div id="key-list"><p class="hint">Loading…</p></div></section>
        <section class="surface panel"><h2>Using a key</h2><div class="terminal"><header><i></i><i></i><i></i><span>publish.sh</span></header><pre><span class="prompt">$</span> curl -X POST "${esc(location.origin)}/api/v1/mods/com.you.mod/releases" \\
    -H "Authorization: Bearer $MONKEFORGE_KEY" \\
    -F version=1.2.0 -F channel=stable \\
    -F downloads_bepinex5=@bin/YourMod.dll</pre></div></section>`;

    const list = body.querySelector('#key-list');

    const load = async () => {
        const result = await api('/api/v1/api-keys');
        list.innerHTML = result.items.length ? result.items.map(k => `<div class="key-item">${icon('key')}<div class="grow"><strong>${esc(k.name)}</strong> <code class="faint">${esc(k.prefix)}…</code><div class="hint">Created ${esc(date(k.created_at))} · ${k.last_used_at ? 'last used ' + esc(date(k.last_used_at)) : 'never used'}</div></div>${k.revoked_at ? '<span class="pill bad">Revoked</span>' : `<button class="button danger small" data-revoke="${esc(k.id)}">Revoke</button>`}</div>`).join('') : '<p class="hint">No keys yet.</p>';
        list.querySelectorAll('[data-revoke]').forEach(b => b.onclick = async () => {
            if (!confirm('Revoke this key? Anything using it will stop working.'))
                return;

            try {
                await api('/api/v1/api-keys/' + encodeURIComponent(b.dataset.revoke), { method: 'DELETE' });
                await load();
            } catch (error) {
                toast(error.message, 'error');
            }
        });
    };

    body.querySelector('#key-form').onsubmit = async event => {
        event.preventDefault();

        try {
            const result = await api('/api/v1/api-keys', { method: 'POST', json: { name: body.querySelector('#key-name').value } });
            body.querySelector('#key-token').value = result.token;
            body.querySelector('#new-key').hidden = false;
            event.target.reset();
            await load();
        } catch (error) {
            toast(error.message, 'error');
        }
    };
    body.querySelector('#key-copy').onclick = () => navigator.clipboard.writeText(body.querySelector('#key-token').value).then(() => toast('Key copied.'));
    body.querySelector('#key-done').onclick = () => {
        body.querySelector('#key-token').value = '';
        body.querySelector('#new-key').hidden = true;
    };

    await load().catch(error => list.innerHTML = `<p class="error">${esc(error.message)}</p>`);
}

function channels(body) {
    if (!auth.user) {
        body.innerHTML = requireSignIn('Unlock a release channel', 'Sign in to redeem a channel code shared by a mod author.');
        return;
    }

    body.innerHTML = `<section class="surface panel">
        <h2>Unlock a release channel</h2>
        <p class="hint">Mod authors can share 25-character codes for private betas. Paste the whole code into the first box.</p>
        <form id="unlock-form">
            <div class="code-boxes">${Array.from({ length: 5 }, (_, i) => `${i ? '<span aria-hidden="true">–</span>' : ''}<input aria-label="Code group ${i + 1} of 5" maxlength="5" autocomplete="off" autocapitalize="characters" spellcheck="false" required>`).join('')}</div>
            <div class="gap"></div>
            <button class="button" type="submit">${icon('lock')}Unlock channel</button>
            <p class="hint" id="unlock-status" role="status"></p>
        </form>
    </section>`;

    const form = body.querySelector('#unlock-form');
    const inputs = [...form.querySelectorAll('input')];

    inputs.forEach((input, i) => {
        input.oninput = () => {
            input.value = input.value.replace(/[^a-z0-9]/gi, '').toUpperCase();

            if (input.value.length === 5)
                inputs[i + 1]?.focus();
        };
        input.onkeydown = event => {
            if (event.key === 'Backspace' && !input.value)
                inputs[i - 1]?.focus();
        };
        input.onpaste = event => {
            const value = event.clipboardData.getData('text').trim();

            if (/^(?:[a-z0-9]{25}|[a-z0-9]{5}(?:-[a-z0-9]{5}){4})$/i.test(value)) {
                event.preventDefault();
                const code = value.replace(/-/g, '').toUpperCase();
                inputs.forEach((box, j) => box.value = code.slice(j * 5, j * 5 + 5));
                inputs[4].focus();
            }
        };
    });

    form.onsubmit = async event => {
        event.preventDefault();
        const status = form.querySelector('#unlock-status');

        try {
            const result = await api('/api/v1/channels/unlock', { method: 'POST', json: { code: inputs.map(i => i.value).join('-') } });
            status.innerHTML = `Unlocked <strong>${esc(result.name)}</strong>. <a href="${esc(result.url)}">Open it -></a>`;
            form.reset();
        } catch (error) {
            status.textContent = error.message;
        }
    };
}

function account(body) {
    if (!auth.user) {
        body.innerHTML = `<section class="surface panel"><h2>Not signed in</h2><p class="hint">Sign in to publish mods, rate, comment and sync settings.</p><a class="button" href="${signInUrl()}">${icon('user')}Sign in</a></section>`;
        return;
    }

    const user = auth.user;
    body.innerHTML = `<section class="surface panel">
            <div class="row">${avatar(user)}<div><strong>${esc(user.display_name)}</strong><div class="hint mono">@${esc(user.username)}${user.discord_username ? ` · Discord @${esc(user.discord_username)}` : ' · Email account'}</div></div><span class="spacer"></span><a class="button subtle" href="${esc(user.profile_url)}">${icon('user')}View profile</a></div>
        </section>
        <section class="surface panel">
            <h2>Account ID</h2>
            <p class="hint">Share this with mod authors who want to add you to a private release channel.</p>
            <div class="copy-field"><input readonly value="${esc(user.id)}" aria-label="Account ID"><button class="button subtle" id="copy-id">${icon('copy')}Copy</button></div>
        </section>
        <section class="surface panel">
            <h2>Session</h2>
            <p class="hint">You stay signed in for 30 days on this browser. Signing out ends this session only. To sign out everywhere, use <a href="/settings/security">Sign-in & security</a>.</p>
            <button class="button danger" id="sign-out-here">${icon('logout')}Sign out</button>
        </section>`;

    body.querySelector('#copy-id').onclick = () => navigator.clipboard.writeText(user.id).then(() => toast('Account ID copied.'));
    body.querySelector('#sign-out-here').onclick = async () => {
        await api('/api/v1/logout', { method: 'POST' });
        location.assign('/browse');
    };
}

// Shows creators what buyers see from their Stripe account, and which fields to change when those would expose them.
function privacyPanel(privacy) {
    const wrong = new Set(privacy.issues);
    const notice = !wrong.size ? ''
        : privacy.grace_ends_at ? `<p class="notice">${esc(fill(copy.privacyGrace, { date: new Date(privacy.grace_ends_at).toLocaleDateString() }))}</p>`
        : `<p class="notice">${esc(copy.privacyProblem)}</p>`;
    const rows = Object.entries(copy.privacyFields).map(([key, label]) => `
        <div class="key-item"><div class="grow"><strong>${esc(label)}</strong><div class="mono">${esc(privacy.expected[key] || '')}</div></div>${wrong.has(key) ? `<span class="pill bad">${esc(copy.privacyWrong)}</span>` : ''}</div>`).join('');

    return `<section class="surface panel">
        <h2>${esc(copy.privacyTitle)}</h2>
        <p class="hint">${esc(fill(copy.privacyIntro, { brand: brand.name }))}</p>
        ${notice}
        ${rows}
        <div class="row"><a class="button ${wrong.size ? '' : 'subtle'}" href="https://dashboard.stripe.com/settings/public" target="_blank" rel="noopener noreferrer">${icon('external')}${esc(copy.privacyEdit)}</a><a class="button subtle" href="/docs/creators/payouts#keeping-your-details-private">${esc(copy.privacyGuide)}</a></div>
    </section>`;
}

async function payouts(body) {
    if (!auth.user) {
        body.innerHTML = requireSignIn(copy.payoutsTitle, fill(copy.payoutsIntro, { brand: brand.name }));
        return;
    }

    const params = new URLSearchParams(location.search);
    const owned = (await myGroups()).filter(g => g.role === 'owner');
    const accounts = [{ id: auth.user.id, name: auth.user.display_name + ' (you)' }, ...owned.map(g => ({ id: g.id, name: g.name + ' · group' }))];
    const selected = accounts.find(a => a.id === params.get('account'))?.id || auth.user.id;
    const query = selected === auth.user.id ? '' : '?account=' + encodeURIComponent(selected);
    const status = await api('/api/v1/payouts' + query);
    const values = { brand: brand.name, fee: status.fee_percent };
    const state = status.charges_enabled && !status.can_sell ? ['bad', copy.payoutsPaused]
        : status.charges_enabled ? ['good', copy.payoutsReady] : status.connected ? ['warn', copy.payoutsIncomplete] : ['', copy.payoutsNotConnected];
    const cur = status.currency;
    const returned = params.get('onboarding');

    body.innerHTML = `
        ${returned === 'done' ? `<p class="notice">${esc(copy.payoutsReturned)}</p>` : returned === 'retry' ? `<p class="notice">${esc(copy.payoutsRetry)}</p>` : ''}
        <section class="surface panel">
            ${accounts.length > 1 ? `<div class="field"><label for="payout-account">${esc(copy.payoutsFor)}</label><select id="payout-account">${accounts.map(a => `<option value="${esc(a.id)}" ${a.id === selected ? 'selected' : ''}>${esc(a.name)}</option>`).join('')}</select></div>` : ''}
            <div class="row"><h2>${esc(copy.payoutsTitle)}</h2><span class="pill ${state[0]}">${esc(state[1])}</span></div>
            <p class="hint">${esc(fill(copy.payoutsIntro, values))}</p>
            <p class="hint">${esc(fill(copy.payoutsFees, values))}</p>
            ${!status.enabled ? `<p class="notice">${esc(copy.storeDisabled)}</p>` : `<div class="row">
                ${status.charges_enabled ? '' : `<button class="button" id="payout-onboard">${icon('external')}${esc(status.connected ? copy.payoutsContinue : copy.payoutsConnect)}</button>`}
                ${status.connected ? `<a class="button subtle" href="https://dashboard.stripe.com" target="_blank" rel="noopener noreferrer">${icon('external')}${esc(copy.payoutsDashboard)}</a>` : ''}
            </div>`}
        </section>
        ${status.connected && status.privacy ? privacyPanel(status.privacy) : ''}
        ${status.connected ? `<div class="stat-strip payout-totals">
            <div><span>${esc(copy.payoutsSales)}</span><strong>${esc(money(status.totals.sales_cents, cur))}</strong></div>
            <div><span>${esc(fill(copy.payoutsFeesPaid, values))}</span><strong>${esc(money(status.totals.fees_cents, cur))}</strong></div>
            <div><span>${esc(copy.payoutsEarnings)}</span><strong>${esc(money(status.totals.earnings_cents, cur))}</strong></div>
            <div><span>${esc(copy.payoutsDonations)}</span><strong>${esc(money(status.totals.donations_cents, cur))}</strong></div>
        </div>
        <section class="surface panel"><h2>${esc(copy.payoutsRecent)}</h2>${status.recent.length ? status.recent.map(r => `
            <div class="key-item">${icon(r.kind === 'purchase' ? 'tag' : r.kind === 'subscription' ? 'layers' : 'heart')}<div class="grow"><strong>${esc(r.kind === 'purchase' ? copy.kindPurchase : r.kind === 'subscription' ? copy.kindSubscription : copy.kindDonation)}</strong>${r.name ? ' · ' + esc(r.name) : ''}<div class="hint">${esc(ago(r.created_at))}</div></div>
            <span class="mono">${esc(money(r.amount_cents - r.fee_cents, r.currency))}</span>${r.status !== 'paid' ? `<span class="pill bad">${esc(copy.statuses[r.status] || r.status)}</span>` : ''}</div>`).join('') : `<p class="hint">${esc(copy.payoutsNone)}</p>`}</section>` : ''}`;

    body.querySelector('#payout-account')?.addEventListener('change', event => {
        location.assign('/settings/payouts' + (event.target.value === auth.user.id ? '' : '?account=' + encodeURIComponent(event.target.value)));
    });
    body.querySelector('#payout-onboard')?.addEventListener('click', async event => {
        event.target.disabled = true;

        try {
            const { url } = await api('/api/v1/payouts/onboard' + query, { method: 'POST' });
            location.assign(url);
        } catch (error) {
            toast(error.message, 'error');
            event.target.disabled = false;
        }
    });
}

async function devices(body) {
    if (!auth.user) {
        body.innerHTML = requireSignIn(copy.devicesTitle, copy.devicesEmpty);
        return;
    }

    const load = async () => {
        const result = await api('/api/v1/injector/devices');

        body.innerHTML = `<section class="surface panel"><h2>${esc(copy.devicesTitle)}</h2><p class="hint">${esc(fill(copy.devicesIntro, { max: result.max_devices }))}</p>
            ${result.items.length ? result.items.map(d => `<div class="key-item">${icon('server')}<div class="grow"><strong>${esc(d.name)}</strong>
                <div class="hint">${esc(d.last_used_at ? fill(copy.devicesLastUsed, { when: ago(d.last_used_at) }) : copy.devicesNeverUsed)}</div></div>
                <button class="button danger small" data-remove="${esc(d.id)}">${esc(copy.devicesRemove)}</button></div>`).join('') : `<p class="hint">${esc(copy.devicesEmpty)}</p>`}</section>`;

        body.querySelectorAll('[data-remove]').forEach(button => button.onclick = async () => {
            if (!confirm(copy.devicesRemoveConfirm))
                return;

            try {
                await api('/api/v1/injector/devices/' + encodeURIComponent(button.dataset.remove), { method: 'DELETE' });
                await load();
            } catch (error) {
                toast(error.message, 'error');
            }
        });
    };

    await load();
}

async function security(body) {
    if (!auth.user) {
        body.innerHTML = requireSignIn('Sign-in & security', 'Sign in to manage your email, password and passkeys.');
        return;
    }

    const load = async () => {
        const account = await api('/api/v1/account');
        const emailAccount = account.provider === 'email';

        body.innerHTML = `
            <section class="surface panel">
                <h2>How you sign in</h2>
                <p class="hint">${emailAccount
                    ? `This is an email account. Sign in with your username or email and password${account.passkeys.length ? ', or with a passkey' : ''}.`
                    : `This account signs in with Discord (@${esc(account.discord_username)})${account.passkeys.length ? ' or a passkey' : ''}.`}</p>
            </section>
            <section class="surface panel">
                <h2>Email address</h2>
                <p class="hint">${emailAccount ? 'Used to sign in and for account emails.' : 'Used for release notices and announcements. Discord shares the address on your Discord account when you sign in.'} Never shown publicly.</p>
                <p>${account.email ? `<strong>${esc(account.email)}</strong> <span class="pill good">Confirmed</span>${account.email_source === 'discord' ? ' <span class="pill">From Discord</span>' : ''}` : '<span class="hint">No confirmed address yet.</span>'}</p>
                ${account.pending_email ? `<p class="notice">We sent a link to <strong>${esc(account.pending_email)}</strong>. Open it to finish changing your address.</p>` : ''}
                ${account.mail_configured ? `<form class="stack" id="email-form">
                    <div class="field"><label for="new-email">${account.email ? 'New email' : 'Email'}</label><input id="new-email" type="email" required maxlength="254" autocomplete="email" spellcheck="false"></div>
                    ${account.has_password ? '<div class="field"><label for="email-password">Current password</label><input id="email-password" type="password" required maxlength="128" autocomplete="current-password"></div>' : ''}
                    <div class="row"><span class="spacer"></span><button class="button" type="submit">Send confirmation link</button></div>
                </form>` : '<p class="notice">Email isn’t set up on this server yet.</p>'}
            </section>
            ${account.has_password ? `<section class="surface panel">
                <h2>Password</h2>
                <form class="stack" id="password-form">
                    <div class="field"><label for="current-password">Current password</label><input id="current-password" type="password" required maxlength="128" autocomplete="current-password"></div>
                    <div class="field"><label for="new-password">New password<span>At least 8 characters</span></label><input id="new-password" type="password" required minlength="8" maxlength="128" autocomplete="new-password"></div>
                    <div class="row"><span class="hint">Changing it signs you out on every other device.</span><span class="spacer"></span><button class="button" type="submit">Change password</button></div>
                </form>
            </section>` : ''}
            <section class="surface panel">
                <div class="row"><h2>Passkeys</h2><span class="spacer"></span>${passkeysSupported() ? `<button class="button small" id="add-passkey">${icon('plus')}Add a passkey</button>` : ''}</div>
                <p class="hint">Sign in with your fingerprint, face, screen lock or a security key instead of ${emailAccount ? 'a password' : 'Discord'}. Passkeys can’t be phished and are stored by your device or password manager.</p>
                ${passkeysSupported() ? '' : '<p class="notice">This browser doesn’t support passkeys.</p>'}
                <div id="passkey-list">${account.passkeys.length ? account.passkeys.map(k => `<div class="key-item">${icon('key')}<div class="grow"><strong>${esc(k.name)}</strong><div class="hint">Added ${esc(date(k.created_at))} · ${k.last_used_at ? 'last used ' + esc(ago(k.last_used_at)) : 'never used'}</div></div>
                    <button class="button ghost small" data-rename="${esc(k.id)}" data-name="${esc(k.name)}">Rename</button><button class="button danger small" data-remove="${esc(k.id)}">Remove</button></div>`).join('') : '<p class="hint">No passkeys yet.</p>'}</div>
            </section>
            <section class="surface panel">
                <h2>Sign out everywhere</h2>
                <p class="hint">Ends every session on other browsers and apps, including Launchpad and the CLI. This browser stays signed in. Access keys keep working; revoke them in <a href="/settings/keys">Access keys</a>.</p>
                <button class="button danger" id="sign-out-all">${icon('logout')}Sign out other devices</button>
            </section>`;

        body.querySelector('#email-form')?.addEventListener('submit', async event => {
            event.preventDefault();

            try {
                const result = await api('/api/v1/account/email', { method: 'PUT', json: { email: body.querySelector('#new-email').value.trim(), password: body.querySelector('#email-password')?.value } });
                toast(`Check ${result.pending_email} for a confirmation link.`);
                await load();
            } catch (error) {
                toast(error.message, 'error');
            }
        });

        body.querySelector('#password-form')?.addEventListener('submit', async event => {
            event.preventDefault();

            try {
                const result = await api('/api/v1/account/password', { method: 'PUT', json: { current_password: body.querySelector('#current-password').value, password: body.querySelector('#new-password').value } });
                auth.csrf_token = result.csrf_token;
                toast('Password changed. Other devices were signed out.');
                event.target.reset();
            } catch (error) {
                toast(error.message, 'error');
            }
        });

        body.querySelector('#add-passkey')?.addEventListener('click', async event => {
            const button = event.currentTarget;
            const name = prompt('Name this passkey so you can recognise it later:', /Mac|iPhone|iPad/.test(navigator.userAgent) ? 'iCloud Keychain' : /Android/.test(navigator.userAgent) ? 'Android phone' : /Windows/.test(navigator.userAgent) ? 'Windows Hello' : 'Passkey');

            if (name === null)
                return;

            button.disabled = true;

            try {
                await registerPasskey(name.trim() || 'Passkey');
                toast('Passkey added. You can use it to sign in now.');
                await load();
            } catch (error) {
                if (!cancelled(error))
                    toast(error.message, 'error');

                button.disabled = false;
            }
        });

        body.querySelectorAll('[data-rename]').forEach(button => button.onclick = async () => {
            const name = prompt('New name for this passkey:', button.dataset.name);

            if (!name?.trim())
                return;

            try {
                await api('/api/v1/passkeys/' + encodeURIComponent(button.dataset.rename), { method: 'PATCH', json: { name: name.trim() } });
                await load();
            } catch (error) {
                toast(error.message, 'error');
            }
        });

        body.querySelectorAll('[data-remove]').forEach(button => button.onclick = async () => {
            if (!confirm('Remove this passkey? You won’t be able to sign in with it any more. Also delete it from your device or password manager.'))
                return;

            try {
                await api('/api/v1/passkeys/' + encodeURIComponent(button.dataset.remove), { method: 'DELETE' });
                await load();
            } catch (error) {
                toast(error.message, 'error');
            }
        });

        body.querySelector('#sign-out-all').onclick = async () => {
            if (!confirm('Sign out every other browser and app?'))
                return;

            try {
                const result = await api('/api/v1/account/sign-out-everywhere', { method: 'POST' });
                auth.csrf_token = result.csrf_token;
                toast('Other devices were signed out.');
            } catch (error) {
                toast(error.message, 'error');
            }
        };
    };

    await load();
}

async function emails(body) {
    if (!auth.user) {
        body.innerHTML = requireSignIn('Email', 'Sign in to choose which emails you get.');
        return;
    }

    const account = await api('/api/v1/account');
    const toggle = (key, title, text) => `<label class="check-card toggle-card"><input type="checkbox" data-pref="${key}" ${account.notifications[key] ? 'checked' : ''}><div><strong>${esc(title)}</strong><div class="hint">${esc(text)}</div></div></label>`;

    body.innerHTML = `<section class="surface panel">
        <h2>What we email you</h2>
        ${account.email ? `<p class="hint">Emails go to <strong>${esc(account.email)}</strong>. <a href="/settings/security">Change it</a></p>` : `<p class="notice">Your account has no confirmed email address, so you won’t get any of these. <a href="/settings/security">Add one</a></p>`}
        <div class="stack">
            ${toggle('releases', 'New releases on mods you manage', 'When anyone uploads a release to a mod you own, or to one owned by a publishing group you’re in. Useful for spotting uploads you didn’t expect.')}
            ${toggle('announcements', `${brand.name} announcements`, 'Occasional news from the staff team.')}
        </div>
        <p class="hint">Account emails, like confirmation links, password resets and security alerts, are always sent.</p>
    </section>`;

    body.querySelectorAll('[data-pref]').forEach(input => input.onchange = async () => {
        try {
            await api('/api/v1/account/notifications', { method: 'PUT', json: { [input.dataset.pref]: input.checked } });
            toast('Email settings saved.');
        } catch (error) {
            input.checked = !input.checked;
            toast(error.message, 'error');
        }
    });
}
