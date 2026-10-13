import { renderSocial } from './social.js';
import type { Installed, Mod } from '../src/types';
import { version } from '../package.json';
import { setupWindowControls, syncWindowPreferences } from './window';
const $ = <T extends HTMLElement = HTMLElement>(s: string) => document.querySelector<T>(s)!;
const esc = (v: unknown) => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
const icons: Record<string, string> = {
  launch: '<path d="m9 4 11 8-11 8z"/>', mods: '<rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/>',
  friends: '<circle cx="9" cy="8" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0v3m1-16a3 3 0 0 1 0 6m2 4a5 5 0 0 1 3 5"/>',
  chat: '<path d="M4 4h16v12H9l-5 4z"/>', groups: '<circle cx="8" cy="8" r="3"/><circle cx="17" cy="8" r="3"/><path d="M2 21v-3a6 6 0 0 1 12 0v3m1-6a5 5 0 0 1 7 5"/>', computer: '<rect x="3" y="3" width="18" height="13" rx="2"/><path d="M8 21h8m-4-5v5"/>',
  settings: '<path d="m9 3-1 3-3 1 1 3-2 2 2 2-1 3 3 1 1 3h4l1-3 3-1-1-3 2-2-2-2 1-3-3-1-1-3z"/><circle cx="11" cy="12" r="3"/>',
  search: '<circle cx="10" cy="10" r="6"/><path d="m15 15 5 5"/>', download: '<path d="M12 3v12m-5-5 5 5 5-5M5 17v4h14v-4"/>',
  arrow: '<path d="M4 12h16m-6-6 6 6-6 6"/>', back: '<path d="M20 12H4m6-6-6 6 6 6"/>', check: '<path d="m5 12 4 4L19 6"/>',
  steam: '<circle cx="16" cy="7" r="4"/><circle cx="16" cy="7" r="2"/><circle cx="7" cy="17" r="3"/><path d="m1 14 6 3 5-8m-2 8 6-6"/>',
  shield: '<path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6z"/><path d="m8 12 3 3 5-6"/>', plus: '<path d="M12 5v14M5 12h14"/>', user: '<circle cx="12" cy="8" r="4"/><path d="M4 22v-2a8 8 0 0 1 16 0v2"/>',
};
const icon = (name: string) => `<svg viewBox="0 0 24 24" aria-hidden="true">${icons[name] || icons.mods}</svg>`;
const count = (n: number) => new Intl.NumberFormat('en', { notation: n >= 10000 ? 'compact' : 'standard', maximumFractionDigits: 1 }).format(n);
const media = (url: string | null) => { if (!url) return ''; const parsed = new URL(url, 'https://monkeforge.org'); return parsed.protocol === 'https:' ? parsed.href : ''; };
let state: any = { installed: [], settings: { accent: '#dc5c3b', channels: {} }, user: null };
let initializing = true;
let currentPage = 'launch', modTab = 'discover', category = '', query = '', sort = 'newest', page = 1;
let currentRoute: string | null = null;
let generation = 0, lastUser = '', lastMods = '', lastLogin = '', pending = false, toastTimer: ReturnType<typeof setTimeout>;
const token = $('meta[name="launchpad-token"]').getAttribute('content')!;
async function request(path: string, body?: unknown): Promise<any> {
  const response = await fetch(path, { method: body === undefined ? 'GET' : 'POST', headers: { 'X-Launchpad-Token': token, ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}) }, ...(body !== undefined ? { body: JSON.stringify(body) } : {}) });
  const data = await response.json() as any; if (!response.ok) throw new Error(data.error || 'Request failed'); return data;
}
const catalog = (path: string) => request('/api/catalog?path=' + encodeURIComponent(path));
function toast(message: string) { $('#toast').textContent = message; $('#toast').hidden = false; clearTimeout(toastTimer); toastTimer = setTimeout(() => $('#toast').hidden = true, 6500); }
setupWindowControls(toast, () => !state.busy && !pending);
window.addEventListener('launchpad-native-error', event => toast((event as CustomEvent<string>).detail));
async function action(fn: () => Promise<void>) { try { await fn(); } catch (e) { toast(e instanceof Error ? e.message : String(e)); } }
function confirm(title: string, description: string): Promise<boolean> {
  const dialog = $<HTMLDialogElement>('#confirm'); $('#confirm-title').textContent = title; $('#confirm-description').textContent = description;
  dialog.showModal(); return new Promise(resolve => { $('#confirm-ok').onclick = () => { dialog.close(); resolve(true); }; $('#confirm-cancel').onclick = () => { dialog.close(); resolve(false); }; dialog.oncancel = () => resolve(false); });
}
function avatar(user: any) { return user.avatar ? `<img class="avatar" src="${esc(media(user.avatar))}" alt="">` : `<span class="avatar">${esc((user.name || user.display_name || user.username || '?').slice(0, 1).toUpperCase())}</span>`; }
function modIcon(mod: Pick<Mod, 'logo' | 'name'>) { return mod.logo ? `<img class="mod-icon" src="${esc(media(mod.logo))}" alt="">` : `<span class="mod-icon">${esc(mod.name.slice(0, 2).toUpperCase())}</span>`; }
function heading(title: string) { return `<div class="heading"><h1>${title}</h1></div>`; }
function empty(title: string, description: string, button = '', name = 'mods') { return `<div class="empty">${icon(name)}<h2>${title}</h2><p>${description}</p>${button}</div>`; }
function setPage(name: string) {
  window.scrollTo({ top: 0 });
  document.documentElement.style.setProperty('--accent', state.settings.accent);
  currentPage = name; generation++; document.querySelectorAll('[data-page]').forEach(el => el.classList.toggle('active', el.getAttribute('data-page') === name));

}
function navigate(name: string) {
  setPage(name); currentRoute = name; location.hash = name;
  if (name === 'launch') renderLaunch();
  else if (name === 'mods') void renderMods();
  else if (['friends', 'chat', 'groups'].includes(name)) void action(async () => { await renderSocial($('#content'), socialApi, state.user, name, toast, 'https://monkeforge.org'); });
  else if (name === 'computer') void action(renderComputer);
  else renderSettings();
}
const landscape = `<div class="hero-art" aria-hidden="true"></div>`;
function renderLaunch() {
  const installed: Installed[] = state.installed.filter((m: Installed) => m.guid !== 'bepinex');
  $('#content').innerHTML = heading(state.user ? `Welcome, ${esc(state.user.username || state.user.discord_username || state.user.display_name || state.user.name)}` : 'Welcome to Launchpad') +
    `<section class="hero">${landscape}<div class="hero-copy"><h2>Launch</h2><button id="launch-game" class="button">${icon('launch')}<span id="launch-label">Launch Gorilla Tag</span></button><div id="launch-message" class="muted" style="font-size:11px;margin-top:12px"></div></div></section>
    <div class="stats"><div class="stat">${icon('steam')}<div><small>Game status</small><strong id="game-status">Checking…</strong></div></div><div class="stat">${icon('mods')}<div><small>Your loadout</small><strong>${installed.length} mods installed</strong></div></div><div class="stat">${icon('shield')}<div><small>Mod Loader</small><strong id="loader-version">${state.loaderVersion ? `BepInEx v${esc(state.loaderVersion)}` : 'Not installed'}</strong></div></div></div>
    <div class="launch-bottom"><section class="panel"><div class="section-title"><h2>Your loadout</h2><button class="text-button" id="manage-mods">Manage mods ↗</button></div>${installed.length ? installed.slice(0, 3).map(m => `<div class="activity-row"><span class="mini-icon">${icon('mods')}</span><div>${esc(m.name)}<small>v${esc(m.version)}</small></div><span class="tag">INSTALLED</span></div>`).join('') : `<div class="activity-row"><span class="mini-icon">${icon('plus')}</span><div>Make the game yours<small>Discover community mods on MonkeForge.</small></div></div><button class="button ghost small" id="discover-first">Explore mods ${icon('arrow')}</button>`}</section><section class="panel"><div class="section-title"><h2>Friends</h2>${icon('friends')}</div><button class="button ghost small" id="open-friends">Find your people ${icon('arrow')}</button></section></div>`;
  $('#launch-game').onclick = () => action(async () => { await request('/api/launch', {}); await refresh(); });
  $('#manage-mods').onclick = () => { modTab = 'installed'; navigate('mods'); };
  $('#discover-first')?.addEventListener('click', () => { modTab = 'discover'; navigate('mods'); });
  $('#open-friends').onclick = () => navigate('friends');

  updateLaunch();
}
function updateLaunch() {
  if (!$('#launch-label')) return;
  const busy = state.busy || state.launching || state.running;
  $<HTMLButtonElement>('#launch-game').disabled = busy;
  $('#launch-label').textContent = state.busy ? 'Preparing your mods…' : state.running ? 'Gorilla Tag is running' : state.launching ? 'Launching through Steam…' : 'Launch Gorilla Tag';
  $('#game-status').textContent = state.running ? 'Running' : state.launching ? 'Launching…' : 'Not running';
  $('#loader-version').textContent = state.loaderVersion ? `BepInEx v${state.loaderVersion}` : 'Not installed';
  $('.heading h1').textContent = state.user ? `Welcome, ${state.user.username || state.user.discord_username || state.user.display_name || state.user.name}` : 'Welcome to Launchpad';
  const message = state.operation || state.launchError || (!state.game ? 'Select your Steam game folder in Settings to get started.' : '');
  $('#launch-message').textContent = message; $('#launch-message').hidden = !message;
}
function card(mod: Mod) {
  return `<a class="mod-card" href="#mod/${encodeURIComponent(mod.id || mod.guid)}" data-mod="${esc(mod.id || mod.guid)}"><div class="card-top">${modIcon(mod)}<span class="tag">${esc(mod.category)}</span></div><h2>${esc(mod.name)}</h2><span class="author">by ${esc(mod.author.name)}${mod.author.is_verified ? '<span class="verified">✓</span>' : ''}</span><p class="card-description">${esc(mod.description.replace(/[#*_`]/g, ''))}</p><div class="card-bottom"><span>${icon('download')}${count(mod.downloads)} downloads</span><span>v${esc(mod.current_version || '—')}</span></div></a>`;
}
function bindCards() { document.querySelectorAll<HTMLElement>('[data-mod]').forEach(el => el.onclick = e => { e.preventDefault(); void renderDetail(el.dataset.mod!); }); }
async function renderMods() {
  const own = ++generation;
  $('#content').innerHTML = heading('Mods') + `<div class="tabs" role="tablist"><button role="tab" aria-selected="${modTab === 'discover'}" class="${modTab === 'discover' ? 'active' : ''}" id="discover-tab">Discover</button><button role="tab" aria-selected="${modTab === 'installed'}" class="${modTab === 'installed' ? 'active' : ''}" id="installed-tab">Installed <span class="count">${state.installed.filter((m: Installed) => m.guid !== 'bepinex').length}</span></button></div><div id="mods-body"></div>`;
  $('#discover-tab').onclick = () => { modTab = 'discover'; void renderMods(); };
  $('#installed-tab').onclick = () => { modTab = 'installed'; void renderMods(); };
  if (modTab === 'installed') { renderInstalled(); return; }
  $('#mods-body').innerHTML = `<form id="search-form" class="toolbar"><div class="search">${icon('search')}<input type="search" id="search" aria-label="Search mods" placeholder="Search mods, creators, or GUIDs…" value="${esc(query)}"></div><select id="sort" aria-label="Sort mods"><option value="newest">Recently updated</option><option value="popular">Most downloaded</option><option value="name">Name: A–Z</option></select><button class="button subtle" type="submit">Search</button></form><div class="categories">${['', 'Gameplay', 'Cosmetic', 'Utilities', 'Accessibility', 'Other'].map(c => `<button data-category="${c}" class="${category === c ? 'active' : ''}">${c || 'All mods'}</button>`).join('')}</div><div id="collection"><div class="loading">Finding your next favorite…</div></div>`;
  $<HTMLSelectElement>('#sort').value = sort;
  $('#search-form').onsubmit = e => { e.preventDefault(); query = $<HTMLInputElement>('#search').value; page = 1; void renderMods(); };
  $('#sort').onchange = () => { sort = $<HTMLSelectElement>('#sort').value; query = $<HTMLInputElement>('#search').value; page = 1; void renderMods(); };
  document.querySelectorAll<HTMLElement>('[data-category]').forEach(el => el.onclick = () => { category = el.dataset.category!; query = $<HTMLInputElement>('#search').value; page = 1; void renderMods(); });
  try {
    const params = new URLSearchParams({ q: query, sort, category, page: String(page), limit: '12' }); if (!category) params.delete('category');
    const data = await catalog('/api/v1/mods?' + params);
    if (own !== generation) return;
    $('#collection').innerHTML = `<div class="results">${data.total} ${data.total === 1 ? 'mod' : 'mods'}${category ? ' in ' + category : ' from the community'}</div>` + (data.items.length ? `<div class="grid">${data.items.map(card).join('')}</div>` : empty('No mods found', 'Try a different search or category.')) + (data.total > 12 ? `<div class="pagination"><button class="button ghost small" id="prev" ${page === 1 ? 'disabled' : ''}>Previous</button><span>${page} / ${Math.ceil(data.total / 12)}</span><button class="button ghost small" id="next" ${page * 12 >= data.total ? 'disabled' : ''}>Next</button></div>` : '');
    bindCards(); $('#prev')?.addEventListener('click', () => { page--; void renderMods(); }); $('#next')?.addEventListener('click', () => { page++; void renderMods(); });
  } catch (e) { if (own === generation) $('#collection').innerHTML = empty('Couldn’t reach MonkeForge', esc((e as Error).message), '<button class="button subtle" id="retry">Try again</button>'); $('#retry')?.addEventListener('click', () => void renderMods()); }
}
function renderInstalled() {
  const installed: Installed[] = state.installed.filter((m: Installed) => m.guid !== 'bepinex');
  $('#mods-body').innerHTML = `<div id="operation" class="notice" ${state.operation ? '' : 'hidden'}>${esc(state.operation)}</div>` + (installed.length ? `<div class="section-title"><span class="results">${installed.length} mods in your loadout</span><button class="button subtle small" id="update-all" ${state.busy ? 'disabled' : ''}>${icon('download')}Update all</button></div>${installed.map(m => `<div class="installed-row"><span class="mini-icon">${icon('mods')}</span><div><h3><button class="text-button" data-mod="${esc(m.guid)}">${esc(m.name)}</button></h3><p>v${esc(m.version)} · ${esc(state.settings.channels[m.guid] || 'stable')}</p></div><div class="friend-actions"><button class="button ghost small" data-update="${esc(m.guid)}" ${state.busy ? 'disabled' : ''}>Update</button><button class="button ghost small" data-remove="${esc(m.guid)}" ${state.busy ? 'disabled' : ''}>Remove</button></div></div>`).join('')}` : empty('A fresh start.', 'Your installed mods will appear here. BepInEx is set up automatically with your first install.', '<button class="button" id="explore">Discover mods '+icon('arrow')+'</button>'));
  bindCards(); $('#explore')?.addEventListener('click', () => { modTab = 'discover'; void renderMods(); });
  document.querySelectorAll<HTMLElement>('[data-update]').forEach(el => el.onclick = () => action(async () => { await install(el.dataset.update!, state.settings.channels[el.dataset.update!] || 'stable'); }));
  document.querySelectorAll<HTMLElement>('[data-remove]').forEach(el => el.onclick = () => action(async () => {
    const mod = installed.find(m => m.guid === el.dataset.remove)!;
    if (!await confirm(`Remove ${mod.name}?`, 'Launchpad will remove the files recorded for this mod.')) return;
    await request('/api/uninstall', { guid: mod.guid }); await refresh(); if (currentPage === 'mods') void renderMods(); toast(`${mod.name} removed`);
  }));
  $('#update-all')?.addEventListener('click', () => action(async () => {
    if (!await confirm('Update your loadout?', 'Launchpad will install each mod’s latest release from its saved channel.')) return;
    for (const mod of installed) await install(mod.guid, state.settings.channels[mod.guid] || 'stable', false);
    toast('Your loadout is up to date.'); if (currentPage === 'mods') void renderMods();
  }));
}
async function install(guid: string, channel: string, ask = true) {
  if (pending) throw new Error('An installation is already in progress');
  if (ask && !await confirm('Install this release?', 'Launchpad will verify the download, install or update BepInEx, and save the mod to your Steam game folder.')) return;
  pending = true;
  try { await syncWindowPreferences(state.settings, true); await request('/api/install', { guid, channel }); await refresh(); toast('Mod installed successfully.'); }
  finally { pending = false; await syncWindowPreferences(state.settings, false); }
  if (currentPage === 'mods' && modTab === 'installed') void renderMods();
}
// Treat even server-rendered Markdown as untrusted at the local application boundary.
function safeHtml(html: string) {
  const template = document.createElement('template'); template.innerHTML = html;
  const allowed = new Set('P H1 H2 H3 H4 H5 H6 UL OL LI STRONG EM DEL BR HR PRE CODE BLOCKQUOTE TABLE THEAD TBODY TR TD TH A IMG'.split(' '));
  for (const el of [...template.content.querySelectorAll('*')]) {
    if (!allowed.has(el.tagName)) { el.replaceWith(document.createTextNode(el.textContent || '')); continue; }
    for (const attr of [...el.attributes]) if (!['href', 'src', 'alt', 'title'].includes(attr.name)) el.removeAttribute(attr.name);
    for (const attr of ['href', 'src']) if (el.hasAttribute(attr)) {
      const url = new URL(el.getAttribute(attr)!, 'https://monkeforge.org');
      if (!['https:', 'http:'].includes(url.protocol)) el.removeAttribute(attr); else el.setAttribute(attr, url.href);
    }
  }
  return template.innerHTML;
}
async function renderDetail(id: string, channel = 'stable') {
  setPage('mods'); currentRoute = `mod/${encodeURIComponent(id)}`; location.hash = currentRoute; const own = ++generation;
  $('#content').innerHTML = '<div class="loading">Loading mod details…</div>';
  try {
    const mod = await catalog(`/api/v1/mods/${encodeURIComponent(id)}?channel=${encodeURIComponent(channel)}`) as Mod;
    if (generation !== own) return;
    const installed = state.installed.find((m: Installed) => m.guid === mod.guid), release = mod.releases[0];
    $('#content').innerHTML = `<button class="back" id="back-mods">${icon('back')}Back to mods</button><div class="detail-head">${modIcon(mod)}<div><h1>${esc(mod.name)}</h1><span class="author">by <button class="text-button" id="author">${esc(mod.author.name)}</button>${mod.author.is_verified ? '<span class="verified">✓</span>' : ''} · ${esc(mod.category)}</span></div><span class="tag" style="margin-left:auto">${count(mod.downloads)} DOWNLOADS</span></div><div class="detail-layout"><div>${mod.screenshots.length ? `<section class="panel gallery"><img id="screenshot" src="${esc(media(mod.screenshots[0]!))}" alt="${esc(mod.name)} screenshot 1"><div class="gallery-controls"><span id="slide-count">1 / ${mod.screenshots.length}</span><div><button class="button ghost small" id="slide-prev" aria-label="Previous screenshot">←</button> <button class="button ghost small" id="slide-next" aria-label="Next screenshot">→</button></div></div></section>` : ''}<section class="panel"><h2>About this mod</h2><div class="markdown">${safeHtml(mod.description_html || esc(mod.description))}</div>${mod.links?.length ? `<div class="links">${mod.links.map(l => `<a href="${esc(media(l.url))}">${esc(l.label)} ↗</a>`).join('')}</div>` : ''}</section><section class="panel"><h2>Mod information</h2><div class="metadata"><div><span>GUID</span><code>${esc(mod.guid)}</code></div><div><span>Author’s Discord</span><strong>@${esc(mod.author.discord_username)}</strong></div><div><span>Capabilities</span><strong>${esc(Object.entries(mod.capabilities).filter(([, v]) => v).map(([k]) => k.replace('_', ' ')).join(', ') || 'None declared')}</strong></div></div></section></div><aside class="detail-aside"><section class="panel"><h2 style="margin-top:10px">${release ? 'Version ' + esc(release.version) : 'No release yet'}</h2><div class="field"><label for="channel">Release channel</label><select id="channel">${mod.channels.map(c => `<option value="${esc(c.name)}" ${c.name === channel ? 'selected' : ''}>${esc(c.name)}</option>`).join('')}</select></div><p>${installed ? 'Installed: v' + esc(installed.version) : 'BepInEx will be set up automatically.'}</p><button class="button" id="install-mod" ${!release || state.busy ? 'disabled' : ''}>${icon('download')}${installed ? 'Reinstall / update' : 'Install mod'}</button><p id="install-status" role="status">${esc(state.operation)}</p>${release?.notes ? `<h2 style="margin-top:22px">Release notes</h2><p style="white-space:pre-wrap">${esc(release.notes)}</p>` : ''}</section><section class="panel"><h2>Version archive</h2>${mod.releases.map(r => `<div class="release"><strong>v${esc(r.version)}</strong><p>${new Date(r.released_at).toLocaleDateString()}</p><p>${esc(r.notes)}</p></div>`).join('')}</section>${mod.source_url ? `<a class="button ghost" href="${esc(media(mod.source_url))}">View source code ↗</a>` : ''}</aside></div>`;
    $('#back-mods').onclick = () => navigate('mods'); $('#author').onclick = () => void renderProfile(mod.author.id);
    if(mod.can_manage){const controls=document.createElement('div');controls.className='actions';controls.innerHTML=`<a class="button subtle" href="https://monkeforge.org/upload?guid=${encodeURIComponent(mod.guid)}&edit=true">Edit listing ↗</a>${mod.can_transfer ? `<a class="button ghost" href="https://monkeforge.org/view/${encodeURIComponent(mod.guid)}">Manage ownership ↗</a>` : ''}`;$('#content').append(controls);}
    $('#channel').onchange = () => void renderDetail(id, $<HTMLSelectElement>('#channel').value);
    $('#install-mod').onclick = () => action(async () => { await install(mod.guid, channel); if (generation === own) void renderDetail(id, channel); });
    $('#install-mod').dataset.unavailable = release ? 'false' : 'true';
    let slide = 0; const update = () => { $<HTMLImageElement>('#screenshot').src = media(mod.screenshots[slide]!); $<HTMLImageElement>('#screenshot').alt = `${mod.name} screenshot ${slide + 1}`; $('#slide-count').textContent = `${slide + 1} / ${mod.screenshots.length}`; };
    $('#slide-prev')?.addEventListener('click', () => { slide = (slide - 1 + mod.screenshots.length) % mod.screenshots.length; update(); });
    $('#slide-next')?.addEventListener('click', () => { slide = (slide + 1) % mod.screenshots.length; update(); });
  } catch (e) { if (generation === own) { $('#content').innerHTML = empty('Couldn’t load this mod', esc((e as Error).message), '<button class="button subtle" id="back-error">Back to mods</button>'); $('#back-error').onclick = () => navigate('mods'); } }
}
async function renderProfile(id: string, profilePage = 1) {
  setPage('mods'); currentRoute = `profile/${encodeURIComponent(id)}`; location.hash = currentRoute; const own = ++generation;
  $('#content').innerHTML = '<div class="loading">Loading creator…</div>';
  try {
    const profile = await catalog('/api/v1/users/' + encodeURIComponent(id));
    const data = await catalog(`/api/v1/mods?author_id=${encodeURIComponent(profile.id)}&limit=12&page=${profilePage}`);
    if (generation !== own) return;
    $('#content').innerHTML = `<button class="back" id="profile-back">${icon('back')}Back to mods</button><div class="profile-head">${avatar(profile)}<div><h1>${esc(profile.display_name || profile.username)}${profile.is_verified ? '<span class="verified">✓</span>' : ''}</h1><span class="muted">@${esc(profile.username)}</span></div></div><div class="profile-description">${esc(profile.description || 'This creator hasn’t added a bio yet.')}</div><div class="links">${(profile.links || []).map((l: any) => `<a class="button ghost small" href="${esc(media(l.url))}">${esc(l.label)} ↗</a>`).join('')}</div>${profile.is_group ? `<section class="panel social-panel"><h2>Group members</h2>${profile.members.map((m: any) => `<div class="social-row"><a href="https://monkeforge.org/profile/${encodeURIComponent(m.id)}">${esc(m.name)}</a><span>${esc(m.role)}</span></div>`).join('')}</section>` : ''}<div class="section-title"><h2>Created by ${esc(profile.display_name || profile.username)}</h2><span class="results">${data.total} mods</span></div>${data.items.length ? `<div class="grid">${data.items.map(card).join('')}</div>` : empty('No published mods yet', 'This creator’s public mods will appear here.')}<div class="pagination"><button class="button ghost small" id="profile-prev" ${profilePage === 1 ? 'disabled' : ''}>Previous</button><span>Page ${profilePage}</span><button class="button ghost small" id="profile-next" ${profilePage * 12 >= data.total ? 'disabled' : ''}>Next</button></div>`;
    if(profile.can_edit){const edit=document.createElement('a');edit.className='button subtle';edit.href='https://monkeforge.org/profile/'+encodeURIComponent(profile.id);edit.textContent='Edit profile ↗';$('.profile-head').append(edit);}
    $('#profile-back').onclick = () => navigate('mods'); bindCards(); $('#profile-prev').onclick = () => void renderProfile(id, profilePage - 1); $('#profile-next').onclick = () => void renderProfile(id, profilePage + 1);
  } catch (e) { if (generation === own) $('#content').innerHTML = empty('Couldn’t load this creator', esc((e as Error).message)); }
}
function renderSettings() {
  $('#content').innerHTML = heading('Settings') + `<form id="settings-form" class="settings-panel"><section class="panel" style="margin-bottom:20px"><h2>Behavior</h2><label class="toggle-row" for="exit-to-tray"><span class="toggle-text">Exit to tray${state.platform === 'linux' ? '<small>Windows only</small>' : ''}</span><span class="toggle"><input id="exit-to-tray" type="checkbox" role="switch" aria-label="Exit to tray" ${state.settings.exitToTray ? 'checked' : ''} ${state.platform !== 'win32' ? 'disabled' : ''}></span></label><label class="toggle-row" for="open-on-startup"><span class="toggle-text">Open on startup</span><span class="toggle"><input id="open-on-startup" type="checkbox" role="switch" aria-label="Open on startup" ${state.settings.openOnStartup ? 'checked' : ''}></span></label></section><section class="panel"><h2>Appearance</h2><div class="field"><label for="accent">Accent color</label><div class="accent-row"><input type="color" id="accent" value="${esc(state.settings.accent)}"><input type="text" id="accent-hex" value="${esc(state.settings.accent)}" pattern="#[a-fA-F0-9]{6}" aria-label="Accent hex color">${['#dc5c3b', '#77b595', '#7da9d8', '#b59cda', '#deb569'].map(c => `<button type="button" class="swatch" data-color="${c}" style="background:${c}" aria-label="Use ${c}"></button>`).join('')}</div></div></section><section class="panel" style="margin-top:20px"><h2>Steam installation</h2><div class="field"><label for="game-path">Gorilla Tag game folder</label><input id="game-path" value="${esc(state.game)}" placeholder="…/steamapps/common/Gorilla Tag"><small>Shared with the MonkeForge CLI through mforge.json. Steam edition only.</small></div>${state.platform === 'linux' ? '<button type="button" class="button subtle" id="proton">Configure BepInEx for Proton</button><p>Close Steam first. Launchpad preserves your existing arguments and saves a backup.</p>' : ''}</section><section class="panel" style="margin-top:20px"><h2>About</h2><p>MonkeForge Launchpad</p><div class="metadata"><div><span>Version</span><strong>${esc(version)}</strong></div></div></section><div class="actions" style="margin-top:20px"><button class="button" type="submit">Save settings ${icon('check')}</button></div></form>`;
  const apply = (c: string) => { $<HTMLInputElement>('#accent').value = c; $<HTMLInputElement>('#accent-hex').value = c; document.documentElement.style.setProperty('--accent', c); };
  $('#accent').oninput = () => apply($<HTMLInputElement>('#accent').value);
  $('#accent-hex').oninput = () => { const c = $<HTMLInputElement>('#accent-hex').value; if (/^#[a-f0-9]{6}$/i.test(c)) apply(c); };
  document.querySelectorAll<HTMLElement>('[data-color]').forEach(el => el.onclick = () => apply(el.dataset.color!));
  $('#settings-form').onsubmit = e => { e.preventDefault(); void action(async () => {
    const body: any = { accent: $<HTMLInputElement>('#accent-hex').value, exitToTray: $<HTMLInputElement>('#exit-to-tray').checked, openOnStartup: $<HTMLInputElement>('#open-on-startup').checked };
    const game = $<HTMLInputElement>('#game-path').value.trim(); if (game && game !== state.game) body.game = game;
    await request('/api/settings', body); await refresh(); toast('Settings saved.');
  }); };
  $('#proton')?.addEventListener('click', () => action(async () => { await request('/api/proton', {}); toast('Proton launch options configured.'); }));
}
function login() { void action(async () => { await request('/api/login', {}); toast('Complete sign-in in your browser. Click your account to cancel.'); await refresh(); }); }
async function refresh() {
  try {
    state = await request('/api/state'); if (currentPage !== 'settings') document.documentElement.style.setProperty('--accent', state.settings.accent);
    $('#account').innerHTML = state.user ? `${avatar(state.user)}<span>${esc(state.user.display_name || state.user.username || state.user.name)}<small>Signed in · click to sign out</small></span>` : `<span class="avatar">${icon('user')}</span><span>${state.loginStatus === 'waiting' ? 'Waiting for sign-in…' : 'Sign in'}<small>${state.loginStatus === 'waiting' ? 'Click to cancel' : 'Connect MonkeForge'}</small></span>`;
    const user = String(state.user?.id || ''), installed = JSON.stringify(state.installed);
    if (lastUser !== user && ['friends','chat','groups'].includes(currentPage)) navigate(currentPage);
    if (!initializing && lastMods !== installed && currentPage === 'launch') renderLaunch();
    lastUser = user; lastMods = installed;
    if (currentPage === 'launch') updateLaunch();
    if ($('#operation')) { $('#operation').textContent = state.operation; $('#operation').hidden = !state.operation; }
    if ($('#install-status')) $('#install-status').textContent = state.operation;
    document.querySelectorAll<HTMLButtonElement>('[data-update], [data-remove], #update-all, #install-mod').forEach(el => { el.disabled = state.busy || pending || el.dataset.unavailable === 'true'; });
    if (lastLogin !== state.loginStatus && (state.loginStatus === 'expired' || state.loginStatus.startsWith('Error:'))) toast(state.loginStatus === 'expired' ? 'Sign-in timed out. Please try again.' : state.loginStatus);
    lastLogin = state.loginStatus;
    await syncWindowPreferences(state.settings, state.busy || pending);
  } catch (e) { toast((e as Error).message); }
}
for (const el of document.querySelectorAll<HTMLElement>('[data-page]')) { const name = el.dataset.page!; el.innerHTML = icon(name) + `<span>${name[0]!.toUpperCase() + name.slice(1)}</span>`; el.onclick = () => navigate(name); }
$('#account').onclick = () => action(async () => { if (state.user) { if (!await confirm('Sign out of MonkeForge?', 'Your installed mods and settings will stay here.')) return; await request('/api/logout', {}); await refresh(); } else if (state.loginStatus === 'waiting') { await request('/api/cancel-login', {}); await refresh(); } else login(); });
document.addEventListener('click', event => {
  const anchor = (event.target as HTMLElement).closest('a'); if (!anchor) return;
  const href = anchor.getAttribute('href') || '';
  if (/^https?:/.test(href)) {
    event.preventDefault(); const url = new URL(href);
    if (url.origin === 'https://monkeforge.org' && url.pathname.startsWith('/view/')) void renderDetail(decodeURIComponent(url.pathname.slice(6)), url.searchParams.get('channel') || 'stable');
    else if (url.origin === 'https://monkeforge.org' && url.pathname.startsWith('/profile/')) void renderProfile(decodeURIComponent(url.pathname.slice(9)));
    else void action(async () => { await request('/api/open', { url: href }); });
  }
});
function route() {
  const hash = location.hash.slice(1);
  if (hash === currentRoute) return;
  if (hash.startsWith('mod/')) void renderDetail(decodeURIComponent(hash.slice(4)));
  else if (hash.startsWith('profile/')) void renderProfile(decodeURIComponent(hash.slice(8)));
  else navigate(['launch', 'mods', 'friends', 'chat', 'groups', 'computer', 'settings'].includes(hash) ? hash : 'launch');
}
$('#content').innerHTML = '<div class="loading">Preparing your Launchpad…</div>';
document.querySelectorAll<HTMLButtonElement>('[data-page], #account').forEach(el => el.disabled = true);
await refresh(); initializing = false;
document.querySelectorAll<HTMLButtonElement>('[data-page], #account').forEach(el => el.disabled = false);
route(); window.addEventListener('hashchange', route);
setInterval(() => void refresh(), 3000);

async function socialApi(path: string, options: RequestInit = {}) { return request('/api/social', { path, method: options.method || 'GET', data: options.body ? JSON.parse(String(options.body)) : undefined }); }
async function renderComputer() {
 const own = ++generation; const data = await request('/api/computer'); if (generation !== own) return;
 $('#content').innerHTML = heading('Computer') + (data.connected && data.settings ? `<form id="computer-form" class="settings-panel panel social-panel"><h2>Gorilla Computer</h2><p>Changes apply in game through Launchpad Connector.</p><div class="field"><label for="computer-name">Name</label><input id="computer-name" value="${esc(data.settings.name)}" pattern="[A-Za-z0-9]{1,12}" maxlength="12" required></div><div class="form-grid">${['red','green','blue'].map(k => `<div class="field"><label for="computer-${k}">${k[0]!.toUpperCase()+k.slice(1)} (0–9)</label><input id="computer-${k}" type="number" min="0" max="9" step="1" required value="${data.settings[k]}"></div>`).join('')}</div><div class="field"><label for="computer-turn">Turning</label><select id="computer-turn">${['NONE','SNAP','SMOOTH'].map(k=>`<option ${data.settings.turn===k?'selected':''}>${k}</option>`).join('')}</select></div><div class="field"><label for="computer-speed">Turn speed (1–9)</label><input id="computer-speed" type="number" min="1" max="9" step="1" required value="${data.settings.turnSpeed}"></div><div class="field"><label for="computer-mic">Microphone</label><select id="computer-mic">${[['ALL CHAT','All'],['PUSH TO TALK','Push to talk'],['PUSH TO MUTE','Push to mute']].map(([k,label])=>`<option value="${k}" ${data.settings.microphone===k?'selected':''}>${label}</option>`).join('')}</select></div><button class="button">Apply to game</button><button type="button" class="button subtle" id="computer-refresh">Refresh</button></form>` : empty('Connect your Gorilla Computer', 'Launch Gorilla Tag with Launchpad Connector installed, then refresh this tab.', '<button class="button" id="computer-refresh">Refresh</button>', 'computer'));
 $('#computer-refresh').onclick = () => void action(renderComputer);
 $('#computer-form')?.addEventListener('submit', e => { e.preventDefault(); void action(async () => { const value = (id: string) => $<HTMLInputElement>('#computer-'+id).value; await request('/api/computer', { name:value('name'),red:Number(value('red')),green:Number(value('green')),blue:Number(value('blue')),turn:value('turn'),turnSpeed:Number(value('speed')),microphone:value('mic') }); toast('Computer settings applied. Name changes follow the game’s checks.'); await renderComputer(); }); });
}
