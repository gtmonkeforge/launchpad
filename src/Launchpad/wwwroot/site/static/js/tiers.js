import { api, brand, date, esc, icon, modIcon, money, toast } from './core.js';
import { fromMinor, storeConfig, toMinor } from './store.js';
import { copy, fill } from './store-copy.js';

export async function subscribe(tierId, button) {
    if (button)
        button.disabled = true;

    toast(copy.buyRedirecting);

    try {
        const { url } = await api('/api/v1/tiers/' + encodeURIComponent(tierId) + '/subscribe', { method: 'POST' });
        location.assign(url);
    } catch (error) {
        toast(error.message, 'error');

        if (button)
            button.disabled = false;
    }
}

// A read-only tier card. Pass signedIn = false to swap the button for a sign-in link.
export function tierCard(tier, { canSubscribe = true, signInHref = null } = {}) {
    const price = fill(copy.tierPerMonth, { price: money(tier.price_cents, tier.currency) });
    const action = tier.subscribed
        ? `<span class="pill good">${icon('check')}${esc(copy.tierSubscribed)}</span>`
        : !canSubscribe ? ''
        : signInHref ? `<a class="button block" href="${esc(signInHref)}">${esc(copy.tierSubscribe)}</a>`
        : `<button class="button block" data-subscribe="${esc(tier.id)}">${esc(copy.tierSubscribe)}</button>`;

    return `<article class="surface tier-card">
        <header>${tier.scope === 'creator' ? `<span class="pill">${esc(fill(copy.tierCreatorBadge, { name: tier.owner_name }))}</span>` : ''}<h3>${esc(tier.name)}</h3><strong class="tier-price">${esc(price)}</strong></header>
        ${tier.benefits.length ? `<ul class="tier-benefits">${tier.benefits.map(b => `<li>${icon('check')}${esc(b)}</li>`).join('')}</ul>` : ''}
        ${tier.mods?.length ? `<p class="hint">${esc(copy.tierMods)}: ${tier.mods.map(m => `<a href="/view/${encodeURIComponent(m.guid)}">${esc(m.name)}</a>`).join(', ')}</p>` : ''}
        ${tier.scope === 'creator' && !tier.mods ? `<p class="hint">${esc(fill(copy.tierCreatorHint, { name: tier.owner_name }))}</p>` : ''}
        ${action}
    </article>`;
}

export function bindSubscribe(root) {
    root.querySelectorAll('[data-subscribe]').forEach(button => button.onclick = () => subscribe(button.dataset.subscribe, button));
}

// Lets a creator add, edit and retire tiers. For a mod pass guid; for a creator membership pass ownerId and their mods.
export async function tierManager(container, { guid = null, ownerId = null, tiers, mods = null, linked = {}, canSell, payoutsUrl, reload }) {
    const config = await storeConfig();
    const hint = fill(guid ? copy.tiersManageModHint : copy.tiersManageCreatorHint, { brand: brand.name, fee: config.fee_percent });

    container.innerHTML = `<h2>${icon('layers')} ${esc(copy.tiersManageTitle)}</h2>
        <p class="hint">${esc(hint)}</p>
        ${!config.enabled ? `<p class="notice">${esc(copy.storeDisabled)}</p>` : !canSell ? `<p class="notice">${esc(copy.pricingNeedsPayouts)} <a href="${esc(payoutsUrl)}">${esc(copy.pricingSetupPayouts)}</a></p>` : ''}
        <div class="tier-admin-list">${tiers.length ? tiers.map(t => `<div class="key-item">${icon('layers')}<div class="grow"><strong>${esc(t.name)}</strong><div class="hint">${esc(fill(copy.tierPerMonth, { price: money(t.price_cents, t.currency) }))}${t.benefits.length ? ' · ' + esc(t.benefits.join(' · ')) : ''}</div></div>
            <button class="button subtle small" data-edit-tier="${esc(t.id)}">${icon('edit')}${esc(copy.tierEdit)}</button><button class="button danger small" data-retire-tier="${esc(t.id)}">${esc(copy.tierRetire)}</button></div>`).join('') : `<p class="hint">${esc(copy.tiersEmpty)}</p>`}</div>
        ${config.enabled && canSell ? `<button class="button subtle" id="tier-add">${icon('plus')}${esc(copy.tierAdd)}</button>` : ''}
        <form class="tier-form" id="tier-form" hidden></form>`;

    const form = container.querySelector('#tier-form');

    const open = tier => {
        const chosen = new Set(tier ? linked[tier.id] || [] : []);
        form.hidden = false;
        form.innerHTML = `
            <div class="grid-2">
                <div class="field"><label for="tier-name">${esc(copy.tierName)}</label><input id="tier-name" required maxlength="60" value="${esc(tier?.name || '')}" placeholder="Supporter"></div>
                <div class="field"><label for="tier-price">${esc(copy.tierPrice)} <span>${esc(config.currency.toUpperCase())}</span></label><input id="tier-price" required inputmode="decimal" value="${tier ? esc(fromMinor(tier.price_cents, config.currency)) : ''}" placeholder="3.00">${tier ? `<p class="hint">${esc(copy.tierPriceNote)}</p>` : ''}</div>
            </div>
            <div class="field"><label for="tier-benefits">${esc(copy.tierBenefits)} <span>${esc(copy.tierBenefitsHint)}</span></label><textarea id="tier-benefits" rows="4" placeholder="Early access to new versions">${esc((tier?.benefits || []).join('\n'))}</textarea></div>
            ${mods ? `<fieldset class="field tier-mods"><legend class="field-label">${esc(copy.tierMods)}</legend>${mods.length ? mods.map(m => `<label class="check"><input type="checkbox" value="${esc(m.guid)}" ${chosen.has(m.guid) ? 'checked' : ''}>${esc(m.name)}</label>`).join('') : `<p class="hint">${esc(copy.tierModsNone)}</p>`}</fieldset>` : ''}
            <div class="row"><span class="spacer"></span><button type="button" class="button ghost" id="tier-cancel">${esc(copy.tierCancel)}</button><button class="button" type="submit">${esc(copy.tierSave)}</button></div>`;

        form.querySelector('#tier-cancel').onclick = () => form.hidden = true;
        form.querySelector('#tier-name').focus();
        form.onsubmit = async event => {
            event.preventDefault();
            const price = toMinor(form.querySelector('#tier-price').value, config.currency);

            if (!Number.isInteger(price))
                return toast(copy.pricingInvalid, 'error');

            const payload = {
                name: form.querySelector('#tier-name').value.trim(),
                price_cents: price,
                benefits: form.querySelector('#tier-benefits').value.split('\n').map(line => line.trim()).filter(Boolean),
            };

            if (mods)
                payload.mods = [...form.querySelectorAll('.tier-mods input:checked')].map(input => input.value);

            if (!tier)
                Object.assign(payload, guid ? { guid } : { owner_id: ownerId });

            try {
                await api(tier ? '/api/v1/tiers/' + encodeURIComponent(tier.id) : '/api/v1/tiers', { method: tier ? 'PATCH' : 'POST', json: payload });
                toast(copy.tierSaved);
                await reload();
            } catch (error) {
                toast(error.message, 'error');
            }
        };
    };

    container.querySelector('#tier-add')?.addEventListener('click', () => open(null));
    container.querySelectorAll('[data-edit-tier]').forEach(button => button.onclick = () => open(tiers.find(t => t.id === button.dataset.editTier)));
    container.querySelectorAll('[data-retire-tier]').forEach(button => button.onclick = async () => {
        if (!confirm(copy.tierRetireConfirm))
            return;

        try {
            await api('/api/v1/tiers/' + encodeURIComponent(button.dataset.retireTier), { method: 'DELETE' });
            toast(copy.tierRetired);
            await reload();
        } catch (error) {
            toast(error.message, 'error');
        }
    });
}

export function openDispute({ paymentId = null, subscriptionId = null, name, done }) {
    const dialog = document.createElement('dialog');
    dialog.className = 'about-dialog';
    dialog.setAttribute('aria-labelledby', 'dispute-title');
    dialog.innerHTML = `<h2 id="dispute-title">${esc(copy.disputeTitle)} · ${esc(name)}</h2>
        <p class="hint">${esc(fill(copy.disputeText, { brand: brand.name }))}</p>
        <form id="dispute-form">
            <div class="field"><label for="dispute-reason">${esc(copy.disputeReason)}</label><textarea id="dispute-reason" required minlength="10" maxlength="2000" rows="5"></textarea></div>
            <p class="error" id="dispute-error" role="alert" hidden></p>
            <div class="row"><span class="spacer"></span><button type="button" class="button ghost" id="dispute-cancel">${esc(copy.tierCancel)}</button><button class="button" type="submit">${esc(copy.disputeSubmit)}</button></div>
        </form>`;

    dialog.addEventListener('close', () => dialog.remove());
    document.body.append(dialog);
    dialog.showModal();

    const form = dialog.querySelector('form');
    form.querySelector('#dispute-cancel').onclick = () => dialog.close();
    form.onsubmit = async event => {
        event.preventDefault();
        const error = form.querySelector('#dispute-error');
        const button = form.querySelector('[type=submit]');
        button.disabled = true;

        try {
            await api('/api/v1/disputes', { method: 'POST', json: { ...(paymentId ? { payment_id: paymentId } : { subscription_id: subscriptionId }), reason: form.querySelector('textarea').value } });
            dialog.close();
            toast(copy.disputeSent);
            done?.();
        } catch (failure) {
            error.textContent = failure.message;
            error.hidden = false;
            button.disabled = false;
        }
    };
}

function staffReply(note) {
    return note ? `<div class="hint">${esc(fill(copy.disputeReply, { note }))}</div>` : '';
}

function disputeLabel(status) {
    if (status === 'open')
        return `<span class="pill warn">${esc(copy.disputeOpen)}</span>`;

    if (status === 'accepted')
        return `<span class="pill good">${esc(copy.disputeAccepted)}</span>`;

    if (status === 'rejected')
        return `<span class="pill">${esc(copy.disputeRejected)}</span>`;

    return '';
}

// The buyer's own purchases and subscriptions, with unsubscribe and dispute actions.
export async function renderLibrary(container) {
    const [purchases, subscriptions, disputes] = await Promise.all([api('/api/v1/purchases'), api('/api/v1/subscriptions'), api('/api/v1/disputes')]);
    const notes = Object.fromEntries(disputes.items.map(d => [d.payment_id || d.subscription_id, d.staff_note]));
    const subs = subscriptions.items;
    const bought = purchases.items;

    const subRow = s => {
        const name = s.scope === 'creator' ? `${s.owner_name} · ${s.tier_name}` : `${s.mod_name || s.guid} · ${s.tier_name}`;
        const when = s.current_period_end ? date(s.current_period_end) : '';
        const state = !s.live ? copy.subEnded : s.status === 'past_due' ? copy.subPastDue : s.cancel_at_period_end ? fill(copy.subEnds, { date: when }) : when ? fill(copy.subRenews, { date: when }) : '';

        return `<div class="key-item">${icon(s.scope === 'creator' ? 'users' : 'layers')}<div class="grow">
                ${s.url ? `<a href="${esc(s.url)}"><strong>${esc(name)}</strong></a>` : `<strong>${esc(name)}</strong>`}
                <div class="hint">${esc(fill(copy.tierPerMonth, { price: money(s.price_cents, s.currency) }))}${state ? ' · ' + esc(state) : ''}</div>${staffReply(notes[s.id])}</div>
            ${disputeLabel(s.dispute_status)}
            ${s.live && !s.cancel_at_period_end ? `<button class="button subtle small" data-unsubscribe="${esc(s.id)}" data-name="${esc(name)}">${esc(copy.unsubscribe)}</button>` : ''}
            ${s.can_dispute ? `<button class="button ghost small" data-dispute-sub="${esc(s.id)}" data-name="${esc(name)}">${esc(copy.reportProblem)}</button>` : ''}
        </div>`;
    };

    const buyRow = item => `<div class="key-item">${modIcon(item)}<div class="grow">
            ${item.url ? `<a href="${esc(item.url)}"><strong>${esc(item.name)}</strong></a>` : `<strong>${esc(item.name)}</strong> <span class="pill">${esc(copy.purchasesUnavailable)}</span>`}
            <div class="hint">${item.amount_cents ? esc(money(item.amount_cents, item.currency)) + ' · ' : ''}${esc(date(item.purchased_at))}</div>${staffReply(notes[item.payment_id])}</div>
        ${disputeLabel(item.dispute_status)}
        ${item.uses_injector && item.available ? `<a class="button subtle small" href="/software/launchpad">${icon('rocket')}${esc(copy.purchasesInjector)}</a>` : ''}
        ${item.can_dispute ? `<button class="button ghost small" data-dispute-payment="${esc(item.payment_id)}" data-name="${esc(item.name)}">${esc(copy.reportProblem)}</button>` : ''}
    </div>`;

    container.innerHTML = `<div class="row"><h2>${esc(copy.libraryTitle)}</h2><span class="hint">${esc(copy.libraryPrivate)}</span></div>
        ${!subs.length && !bought.length ? `<p class="hint">${esc(copy.libraryEmpty)}</p>` : ''}
        ${subs.length ? `<h3 class="subhead">${esc(copy.librarySubscriptions)}</h3>${subs.map(subRow).join('')}` : ''}
        ${bought.length ? `<h3 class="subhead">${esc(copy.libraryPurchases)}</h3>${bought.map(buyRow).join('')}` : ''}`;

    const reload = () => renderLibrary(container);

    container.querySelectorAll('[data-unsubscribe]').forEach(button => button.onclick = async () => {
        if (!confirm(fill(copy.unsubscribeConfirm, { name: button.dataset.name })))
            return;

        button.disabled = true;

        try {
            const result = await api('/api/v1/subscriptions/' + encodeURIComponent(button.dataset.unsubscribe) + '/cancel', { method: 'POST' });
            toast(fill(copy.unsubscribed, { date: result.ends_at ? date(result.ends_at) : '—' }));
            await reload();
        } catch (error) {
            toast(error.message, 'error');
            button.disabled = false;
        }
    });
    container.querySelectorAll('[data-dispute-sub]').forEach(button => button.onclick = () => openDispute({ subscriptionId: button.dataset.disputeSub, name: button.dataset.name, done: reload }));
    container.querySelectorAll('[data-dispute-payment]').forEach(button => button.onclick = () => openDispute({ paymentId: button.dataset.disputePayment, name: button.dataset.name, done: reload }));
}
