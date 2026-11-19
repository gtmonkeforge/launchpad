import { api, app, date, esc, icon, setTitle, size } from '../core.js';
import { shieldRing, shieldState } from './mod.js';

export async function render(id) {
    const channel = new URLSearchParams(location.search).get('channel') || 'stable';
    const query = '?channel=' + encodeURIComponent(channel);
    const [data, mod] = await Promise.all([
        api('/api/v1/mods/' + encodeURIComponent(id) + '/monkeshield-report' + query),
        api('/api/v1/mods/' + encodeURIComponent(id) + query).catch(() => null),
    ]);
    const report = data.report;
    const s = shieldState(report);
    const name = mod?.name || data.guid;
    const checks = (report.artifacts || []).flatMap(a => (a.report.checks || []).map(c => ({ ...c, artifact: a.name })));
    const failing = checks.filter(c => c.status === 'Fail').length;

    setTitle('MonkeShield · ' + name);

    app.innerHTML = `
        <a class="breadcrumb" href="/view/${encodeURIComponent(data.guid)}${query}">${icon('back')}${esc(name)}</a>
        <div class="page-head"><div><h1>${esc(name)}</h1><p class="mono">${data.version ? 'v' + esc(data.version) + ' · ' : ''}${esc(data.channel)}${report.scanned_at ? ' · scanned ' + esc(date(report.scanned_at)) : ''}</p></div>
            <button class="button subtle" id="export">${icon('download')}Export JSON</button></div>
        <div class="shield-layout">
            <section class="surface panel shield-summary">${shieldRing(report)}<span class="pill ${s.state === 'pass' ? 'good' : s.state === 'fail' ? 'bad' : ''}">${esc(s.label)}</span></section>
            <section class="surface panel">
                <h2>${s.complete ? (report.eligible ? 'Meets the automatic approval policy' : 'This release needs a manual review') : 'Analysis is incomplete or still running'}</h2>
                <dl class="details-list">
                    <div><dt>Checks</dt><dd class="mono">${report.total_checks ?? 0} total · ${report.failed_checks ?? 0} failed</dd></div>
                    <div><dt>Assemblies</dt><dd class="mono">${(report.artifacts || []).length}</dd></div>
                    <div><dt>Threshold</dt><dd class="mono">&lt; ${report.threshold_percent ?? 2.5}% failed per assembly</dd></div>
                </dl>
                ${(report.diagnostics || []).map(d => `<p class="notice">${esc(d)}</p>`).join('')}
                <p class="hint">Inconclusive checks count as passing. Unevaluated checks require review. ${s.complete ? '' : 'Refresh to check for results.'}</p>
            </section>
        </div>
        <div class="toolbar">
            <div class="segmented" role="tablist"><button role="tab" aria-selected="true" data-view="checks">Checks</button><button role="tab" aria-selected="false" data-view="details">Assemblies</button></div>
            <label class="check"><input type="checkbox" id="only-failing" ${failing ? '' : 'disabled'}>Only failing (${failing})</label>
            <span class="spacer"></span>
            <label class="sr-only" for="check-filter">Filter checks</label>
            <input id="check-filter" type="search" placeholder="Filter checks…">
        </div>
        <div id="view-checks">${(report.artifacts || []).map(a => `<section class="surface panel"><h2 class="mono">${esc(a.name)}</h2><div class="check-grid">
            ${(a.report.checks || []).map(c => {
                const pass = c.status === 'Pass' || c.status === 'Inconclusive';
                return `<details class="check-card ${c.status === 'Fail' ? 'fail' : ''}" data-status="${esc(c.status)}" data-text="${esc(`${c.title} ${c.ruleId} ${c.category}`.toLowerCase())}">
                    <summary><span><strong>${esc(c.title)}</strong><small>${esc(c.ruleId)} · ${esc(c.category)}</small></span><span class="pill ${pass ? 'good' : c.status === 'Fail' ? 'bad' : ''}">${pass ? 'Pass' : c.status === 'Fail' ? 'Fail' : 'Not evaluated'}</span></summary>
                    <p>${esc(c.explanation)}</p>
                    ${c.status === 'Inconclusive' ? '<p>Scanner result: inconclusive. Counted as passing.</p>' : ''}
                    ${(c.evidence || []).map(e => `<p><code>${esc(e.location)}</code> ${esc(e.detail)}</p>`).join('')}
                </details>`;
            }).join('') || '<p class="hint">No checks were reported for this assembly.</p>'}
        </div></section>`).join('') || '<div class="empty"><h2>No assemblies scanned</h2><p>This release has not been analysed yet.</p></div>'}</div>
        <div id="view-details" hidden>${(report.artifacts || []).map(a => `<section class="surface panel"><h2 class="mono">${esc(a.name)}</h2><dl class="details-list">
            <div><dt>Status</dt><dd>${esc(a.report.status)} · ${esc(a.report.verdict || 'No verdict')}</dd></div>
            <div><dt>SHA-256</dt><dd class="checksum">${esc(a.report.sha256)}</dd></div>
            <div><dt>Size</dt><dd class="mono">${a.report.size ? size(a.report.size) : 'Unknown'}</dd></div>
            <div><dt>Scanner</dt><dd class="mono">${esc(a.report.scannerVersion || 'Unknown')}</dd></div>
        </dl>${(a.report.diagnostics || []).map(d => `<p class="notice">${esc(d)}</p>`).join('')}</section>`).join('')}</div>
        <p class="hint">Static analysis is not a guarantee of safety. Reports reflect the latest release in this channel.</p>`;

    const filter = app.querySelector('#check-filter');
    const onlyFailing = app.querySelector('#only-failing');
    const apply = () => app.querySelectorAll('[data-text]').forEach(card => {
        card.hidden = !card.dataset.text.includes(filter.value.toLowerCase()) || (onlyFailing.checked && card.dataset.status !== 'Fail');
    });

    filter.oninput = apply;
    onlyFailing.onchange = apply;
    app.querySelectorAll('[data-view]').forEach(b => b.onclick = () => {
        app.querySelectorAll('[data-view]').forEach(t => t.setAttribute('aria-selected', String(t === b)));
        app.querySelector('#view-checks').hidden = b.dataset.view !== 'checks';
        app.querySelector('#view-details').hidden = b.dataset.view !== 'details';
    });
    app.querySelector('#export').onclick = () => {
        const url = URL.createObjectURL(new Blob([JSON.stringify(report, null, 2)], { type: 'application/json' }));
        const link = document.createElement('a');
        link.href = url;
        link.download = `monkeshield-${data.guid}-${data.channel}.json`;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    };
}
