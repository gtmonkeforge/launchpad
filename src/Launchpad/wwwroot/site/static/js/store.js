import { api, auth, brand, esc, money, signInUrl, toast, zeroDecimal } from './core.js';
import { copy, fill } from './store-copy.js';

let configRequest;

export function storeConfig() {
    configRequest ??= api('/api/v1/store').catch(() => ({ enabled: false, currency: 'usd', fee_percent: 10, min_donation_cents: 100, max_donation_cents: 50000 }));
    return configRequest;
}

export { money };

export function toMinor(text, currency = 'usd') {
    const value = Number(String(text).replace(/[^\d.]/g, ''));
    return Number.isFinite(value) ? Math.round(zeroDecimal.has(currency) ? value : value * 100) : NaN;
}

export function fromMinor(minor, currency = 'usd') {
    return zeroDecimal.has(currency) ? String(minor) : (minor / 100).toFixed(2);
}

export async function startCheckout(guid, button) {
    if (button)
        button.disabled = true;

    toast(copy.buyRedirecting);

    try {
        const { url } = await api('/api/v1/mods/' + encodeURIComponent(guid) + '/checkout', { method: 'POST' });
        location.assign(url);
    } catch (error) {
        toast(error.message, 'error');

        if (button)
            button.disabled = false;
    }
}

// recipient is a user ID, or 'platform' for donations to the site itself.
export async function openDonate({ recipient, name, guid }) {
    const config = await storeConfig();
    const platform = recipient === 'platform';
    const presets = [300, 500, 1000, 2500];
    const dialog = document.createElement('dialog');
    dialog.className = 'about-dialog donate-dialog';
    dialog.setAttribute('aria-labelledby', 'donate-title');

    const title = platform ? fill(copy.donatePlatformTitle, { brand: brand.name }) : fill(copy.donateTitle, { name });
    const text = platform ? fill(copy.donatePlatformText, { brand: brand.name }) : fill(copy.donateText, { name, brand: brand.name });

    dialog.innerHTML = `
        <h2 id="donate-title">${esc(title)}</h2>
        <p class="hint">${esc(text)}</p>
        ${!config.enabled ? `<p class="notice">${esc(copy.storeDisabled)}</p>` : auth.user ? `<form id="donate-form">
            <fieldset class="donate-amounts"><legend class="field-label">${esc(copy.donateAmount)}</legend>
                ${presets.map((p, i) => `<label class="donate-amount"><input type="radio" name="amount" value="${p}" ${i === 1 ? 'checked' : ''}><span>${esc(money(p, config.currency))}</span></label>`).join('')}
            </fieldset>
            <div class="field"><label for="donate-custom">${esc(copy.donateCustom)}</label><input id="donate-custom" inputmode="decimal" placeholder="${esc(fromMinor(config.min_donation_cents, config.currency))}"></div>
            <p class="error" id="donate-error" role="alert" hidden></p>
            <div class="row"><span class="spacer"></span><button type="button" class="button ghost" value="cancel" id="donate-cancel">Cancel</button><button class="button" type="submit">${esc(copy.donateSubmit)}</button></div>
        </form>` : `<div class="row"><span class="spacer"></span><a class="button" href="${signInUrl()}">${esc(copy.donateSignIn)}</a></div>`}`;

    dialog.addEventListener('close', () => dialog.remove());
    dialog.addEventListener('click', event => {
        if (event.target === dialog)
            dialog.close();
    });
    document.body.append(dialog);
    dialog.showModal();

    const form = dialog.querySelector('#donate-form');

    if (!form)
        return;

    const custom = form.querySelector('#donate-custom');
    custom.oninput = () => form.querySelectorAll('[name=amount]').forEach(r => r.checked = false);
    form.querySelectorAll('[name=amount]').forEach(r => r.onchange = () => custom.value = '');
    form.querySelector('#donate-cancel').onclick = () => dialog.close();

    form.onsubmit = async event => {
        event.preventDefault();
        const error = form.querySelector('#donate-error');
        const picked = form.querySelector('[name=amount]:checked');
        const amount = custom.value.trim() ? toMinor(custom.value, config.currency) : Number(picked?.value);
        error.hidden = true;

        if (!Number.isInteger(amount) || amount < config.min_donation_cents || amount > config.max_donation_cents) {
            error.textContent = fill(copy.donateRange, { min: money(config.min_donation_cents, config.currency), max: money(config.max_donation_cents, config.currency) });
            error.hidden = false;
            return;
        }

        const button = form.querySelector('[type=submit]');
        button.disabled = true;

        try {
            const { url } = await api('/api/v1/donations', { method: 'POST', json: { recipient, amount_cents: amount, ...(guid ? { guid } : {}) } });
            location.assign(url);
        } catch (failure) {
            error.textContent = failure.message;
            error.hidden = false;
            button.disabled = false;
        }
    };
}

// Shows the result of a Stripe redirect once, then tidies the URL.
export function consumeReturn(onPurchase) {
    const url = new URL(location.href);
    const checkout = url.searchParams.get('checkout');
    const donation = url.searchParams.get('donation');

    if (!checkout && !donation)
        return;

    if (checkout === 'success')
        onPurchase?.();
    else if (checkout === 'cancelled')
        toast(copy.purchaseCancelled);

    if (donation === 'success')
        toast(copy.donateThanks);
    else if (donation === 'cancelled')
        toast(copy.donateCancelled);

    url.searchParams.delete('checkout');
    url.searchParams.delete('donation');
    history.replaceState(null, '', url.pathname + url.search + url.hash);
}
