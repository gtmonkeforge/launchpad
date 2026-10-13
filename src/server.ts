import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { Connector, validateComputer } from './connector';
import { MonkeForge } from './api';
import { ModManager } from './mods';
import { atomicJson, configDir, defaults, normalizeSettings, readJson, settingsFile, type Settings } from './config';
import { registerStartup, type LaunchCommand } from './startup';
import { installedLoaderVersion } from './loader';
import { loadAssets } from './assets';
import { APP_ID, configureProton, discoverGame, gameRunning, openExternal } from './steam';
const api = new MonkeForge();
const launchCommandPromise = new Promise<LaunchCommand>(resolve => { self.onmessage = event => resolve(event.data.launchCommand); });
await api.restoreSession();
const saved = await readJson<Settings>(settingsFile, { ...defaults, channels: {} });
const settings = normalizeSettings(saved);
const launchCommand = await launchCommandPromise;
const selected = await readJson(join(configDir, 'mforge.json'), { path: '' });
const mods = new ModManager(api, selected.path || await discoverGame());
const secret = crypto.randomUUID();
let launchAt = 0, launchError = '', running = false;
let lastStatus = 0;
const assets = await loadAssets();
const connector = new Connector();
await connector.publishDiscovery(mods.game);
let socialPolling = false;
async function friends(path: string, options: RequestInit = {}) {
  if (!api.user) throw new Error('Sign in with MonkeForge to use Friends');
  const target = path === '/requests' ? '/friends/requests' : path;
  return api.json('/api/v1' + target, { ...options, headers:{'Content-Type':'application/json'} });
}
async function socialPoll() {
  if (socialPolling) return; socialPolling = true;
  try {
    if (!api.user) return;
    const userId=api.user.id;
    await friends('/presence',{method:'PUT',body:JSON.stringify({room_code:connector.room})});
    if (connector.connected) {
      const result=await api.json('/api/v1/notifications');
      for (const notification of result.items) {
        if (api.user?.id !== userId) return;
        await connector.notify({ ...notification,accent:settings.accent });
        await friends('/notifications/read',{method:'POST',body:JSON.stringify({through:notification.id})});
      }
    }
  } catch {} finally { socialPolling=false; }
}
setInterval(() => void socialPoll(), 10000);
void socialPoll();
const server = Bun.serve({ hostname: '127.0.0.1', port: Number(process.env.LAUNCHPAD_PORT || 0), fetch: async request => {
  const url = new URL(request.url);
  if (url.host !== `127.0.0.1:${server.port}`) return new Response('Invalid host', { status: 403 });
  try {
    if (url.pathname.startsWith('/api/')) {
      if (request.headers.get('X-Launchpad-Token') !== secret || (request.headers.get('Origin') && request.headers.get('Origin') !== server.url.origin)) return new Response('Forbidden', { status: 403 });
      const body = request.method === 'POST' ? await request.json() as any : {};
      let result: any;
      if (request.method === 'GET' && url.pathname === '/api/state') {
        if (Date.now() - lastStatus > 2500) { running = await gameRunning(); lastStatus = Date.now(); }
        if (running) { launchAt = 0; launchError = ''; }
        if (launchAt && Date.now() - launchAt > 60000) { launchAt = 0; launchError = 'Steam did not start the game within one minute. Check Steam and try again.'; }
        const installed = (await mods.manifest()).installed;
        const loaderVersion = await installedLoaderVersion(mods.game, installed.find(m => m.guid === 'bepinex')?.version);
        result = { game: mods.game, platform: process.platform, running, launching: !!launchAt, launchError, loaderVersion, installed, operation: mods.status, busy: mods.busy, settings, user: api.user, loginStatus: api.loginStatus };
      } else if (request.method === 'GET' && url.pathname === '/api/catalog') {
        const path = url.searchParams.get('path') || '/api/v1/mods';
        if (!/^\/api\/v1\/(mods(?:\/[^/?]+(?:\/(ratings|comments))?)?|users\/[^/?]+)(\?|$)/.test(path)) throw new Error('Unsupported catalog endpoint');
        result = await api.json(path);
      } else if (request.method === 'POST' && url.pathname === '/api/login') { await openExternal(api.startLogin()); result = { ok: true }; }
      else if (request.method === 'POST' && url.pathname === '/api/cancel-login') { api.cancelLogin(); result = { ok: true }; }
      else if (request.method === 'POST' && url.pathname === '/api/logout') { if (api.user) await friends('/presence',{method:'PUT',body:JSON.stringify({room_code:null})}).catch(()=>{}); await api.logout(); result = { ok: true }; }
      else if (request.method === 'POST' && url.pathname === '/api/install') {
        result = await mods.install(String(body.guid), String(body.channel || 'stable'));
        settings.channels[result.guid] = body.channel || 'stable'; await atomicJson(settingsFile, settings);
      } else if (request.method === 'POST' && url.pathname === '/api/uninstall') { await mods.uninstall(String(body.guid)); result = { ok: true }; }
      else if (request.method === 'POST' && url.pathname === '/api/launch') {
        if (!mods.game) throw new Error('Select your Steam game folder in Settings');
        if (mods.busy) throw new Error('Wait for mod installation to finish');
        if (!await gameRunning()) { launchAt = Date.now(); launchError = ''; try { await openExternal(`steam://rungameid/${APP_ID}`); } catch (e) { launchAt = 0; throw e; } }
        result = { ok: true };
      } else if (request.method === 'POST' && url.pathname === '/api/proton') { await configureProton(); result = { ok: true }; }
      else if (request.method === 'POST' && url.pathname === '/api/settings') {
        if (mods.busy) throw new Error('Wait for the current mod operation');
        const next = { ...settings };
        if (body.game !== undefined) {
          if (!existsSync(join(String(body.game), 'Gorilla Tag.exe'))) throw new Error('This folder does not contain Gorilla Tag.exe');
        }
        if (body.accent !== undefined) { if (!/^#[a-f0-9]{6}$/i.test(body.accent)) throw new Error('Accent must be a six-digit hex color'); next.accent = body.accent; }
        for (const key of ['exitToTray', 'openOnStartup'] as const) {
          if (body[key] !== undefined) { if (typeof body[key] !== 'boolean') throw new Error(`${key} must be a boolean`); next[key] = body[key]; }
        }
        if (next.exitToTray && process.platform !== 'win32') throw new Error('Exit to tray is available only on Windows');
        const rollback = body.openOnStartup !== undefined ? await registerStartup(next.openOnStartup, launchCommand) : null;
        try { await atomicJson(settingsFile, next); } catch (error) { await rollback?.(); throw error; }
        Object.assign(settings, next);
        if (body.game !== undefined) { mods.game = String(body.game); await atomicJson(join(configDir, 'mforge.json'), { ...selected, path: mods.game }); await connector.publishDiscovery(mods.game); }
        result = { ok: true };
      } else if (request.method === 'POST' && url.pathname === '/api/open') { await openExternal(String(body.url)); result = { ok: true }; }
      else if (url.pathname === '/api/computer' && request.method === 'GET') { if (connector.connected) await connector.command('computer-get', {}); result = { connected:connector.connected, settings:connector.settings }; }
      else if (url.pathname === '/api/computer' && request.method === 'POST') result = await connector.command('computer-set', validateComputer(body));
      else if (url.pathname === '/api/social' && request.method === 'POST') {
        if (!api.user) throw new Error('Sign in with MonkeForge');
        if (typeof body.path !== 'string' || !/^\/api\/v1\/(friends(?:\/requests|\/[^/?]+)?|presence|groups(?:\/[^/?]+\/members(?:\/[^/?]+)?)?|users\/[^/?]+|chat(?:\/[^/?]+\/(?:messages|accept|decline))?)(?:\?before=[0-9]+)?$/.test(body.path) || !['GET','POST','PUT','DELETE'].includes(body.method)) throw new Error('Unsupported social endpoint');
        const data=body.data;
        if (body.path === '/api/v1/presence' && ['friends','none'].includes(data?.visibility)) { result=await friends('/presence',{method:'PUT',body:JSON.stringify({room_code:connector.room,visibility:data.visibility})}); }
        else result=await api.json(body.path,{method:body.method,headers:{'Content-Type':'application/json'},...(body.method==='GET'?{}:{body:JSON.stringify(data || {})})});
      }
      else if (url.pathname === '/api/friends') result = await friends('/friends');
      else if (request.method === 'POST' && url.pathname === '/api/friend-request') result = await friends('/requests', { method: 'POST', body: JSON.stringify({ user_id: body.user_id }) });
      else if (request.method === 'POST' && url.pathname === '/api/friend-action') {
        if (!['accept', 'remove'].includes(body.action)) throw new Error('Invalid friend action');
        result = await friends(`/friends/${encodeURIComponent(body.user_id)}`, { method: body.action === 'accept' ? 'PUT' : 'DELETE' });
      } else if (request.method === 'POST' && url.pathname === '/api/join') result = await friends(`/friends/${encodeURIComponent(body.user_id)}/join`, { method: 'POST' });
      else if (request.method === 'POST' && url.pathname === '/api/presence') result = await friends('/presence', { method: 'PUT', body: JSON.stringify({ status: body.status, room_code: body.room_code, joinable: body.joinable }) });
      else return new Response('Not found', { status: 404 });
      return Response.json(result ?? { ok: true }, { headers: { 'Cache-Control': 'no-store' } });
    }
    if (request.method !== 'GET') return new Response('Not found', { status: 404 });
    if (url.pathname === '/app.js') return new Response(assets.javascript, { headers: { 'Content-Type': 'text/javascript', 'Cache-Control': 'no-store' } });
    if (url.pathname === '/style.css') return new Response(assets.style, { headers: { 'Content-Type': 'text/css' } });
    if (url.pathname === '/chrome.css') return new Response(assets.chrome, { headers: { 'Content-Type': 'text/css' } });
    if (url.pathname === '/logo.svg') return new Response(assets.logo, { headers: { 'Content-Type': 'image/svg+xml' } });
    if (url.pathname === '/launch-header.png') return new Response(Buffer.from(assets.launchHeader, 'base64'), { headers: { 'Content-Type': 'image/png' } });
    if (url.pathname === '/') {
      const html = assets.html.replace('__TOKEN__', secret);
      return new Response(html, { headers: { 'Content-Type': 'text/html', 'Cache-Control': 'no-store', 'Content-Security-Policy': "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' https: data:; connect-src 'self'; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'self'" } });
    }
    return new Response('Not found', { status: 404 });
  } catch (error) { return Response.json({ error: error instanceof Error ? error.message : 'Request failed' }, { status: 400 }); }
}});
postMessage({ url: server.url.href });
