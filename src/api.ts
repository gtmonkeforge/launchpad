import { createHash, randomBytes, timingSafeEqual } from 'node:crypto';
import { readFile, rm } from 'node:fs/promises';
import { join } from 'node:path';
import { atomicJson, configDir } from './config';
export const BASE = 'https://monkeforge.org';
class SessionError extends Error {
  constructor(message: string, readonly status: number) { super(message); }
}
export class MonkeForge {
  constructor(private readonly sessionFile = join(configDir, 'launchpad/session.json')) {}
  cookie = ''; csrf = ''; user: Record<string, any> | null = null;
  private listener: ReturnType<typeof Bun.serve> | null = null;
  private timer: ReturnType<typeof setTimeout> | null = null;
  loginStatus = 'idle';
  private clearSession() { this.cookie = ''; this.csrf = ''; this.user = null; this.loginStatus = 'idle'; }
  async restoreSession() {
    let saved: any;
    try { saved = JSON.parse(await readFile(this.sessionFile, 'utf8')); }
    catch (error: any) {
      if (error.code === 'ENOENT') return;
      if (error instanceof SyntaxError) { await rm(this.sessionFile, { force: true }); return; }
      throw error;
    }
    if (typeof saved?.cookie !== 'string' || !saved.cookie || /[\r\n]/.test(saved.cookie)) {
      await rm(this.sessionFile, { force: true }); return;
    }
    this.cookie = saved.cookie;
    try {
      const session = await this.json('/api/v1/session');
      if (!session.user || typeof session.csrf_token !== 'string' || !session.csrf_token) {
        await rm(this.sessionFile, { force: true }); this.clearSession(); return;
      }
      this.user = session.user; this.csrf = session.csrf_token; this.loginStatus = 'signed-in';
    } catch (error) {
      this.clearSession();
      // Keep saved credentials through temporary network or service failures.
      if (error instanceof SessionError && (error.status === 401 || error.status === 403)) await rm(this.sessionFile, { force: true });
    }
  }
  async request(path: string, options: RequestInit = {}) {
    const url = new URL(path, BASE);
    if (url.origin !== BASE) throw new Error('Unexpected MonkeForge URL');
    const headers = new Headers(options.headers);
    if (this.cookie) headers.set('Cookie', this.cookie);
    if (options.method && options.method !== 'GET') headers.set('X-CSRF-Token', this.csrf);
    const response = await fetch(url, { ...options, headers, redirect: 'error', signal: AbortSignal.timeout(30000) });
    if (!response.ok) { const error = await response.json().catch(() => ({})) as { error?: string }; throw new SessionError(error.error || `MonkeForge returned ${response.status}`, response.status); }
    return response;
  }
  async json(path: string, options?: RequestInit): Promise<any> { return (await this.request(path, options)).json(); }
  cancelLogin() {
    this.listener?.stop(); this.listener = null;
    if (this.timer) clearTimeout(this.timer);
    this.timer = null;
    if (this.loginStatus === 'waiting') this.loginStatus = 'cancelled';
  }
  startLogin() {
    this.cancelLogin();
    const verifier = randomBytes(32).toString('base64url'), state = randomBytes(32).toString('base64url');
    const challenge = createHash('sha256').update(verifier).digest('base64url');
    let consumed = false; this.loginStatus = 'waiting';
    const listener = Bun.serve({ hostname: '127.0.0.1', port: 0, fetch: async request => {
      const url = new URL(request.url), received = url.searchParams.get('state') || '', code = url.searchParams.get('code') || '';
      if (url.pathname !== '/callback' || request.method !== 'GET' || consumed || url.searchParams.getAll('state').length !== 1 || url.searchParams.getAll('code').length !== 1 || !/^[\w-]{43}$/.test(received) || received.length !== state.length || !timingSafeEqual(Buffer.from(received), Buffer.from(state)) || !/^[\w-]{43}$/.test(code)) return new Response('Invalid login callback', { status: 400 });
      consumed = true;
      try {
        const response = await fetch(`${BASE}/api/v1/auth/native/exchange`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ code, code_verifier: verifier, redirect_uri: redirect }), signal: AbortSignal.timeout(15000), redirect: 'error' });
        const data = await response.json() as any;
        if (!response.ok) throw new Error(data.error || 'Sign-in failed');
        const cookie = response.headers.getSetCookie().map(c => c.split(';')[0]).join('; ');
        if (!cookie) throw new Error('No session cookie returned');
        await atomicJson(this.sessionFile, { cookie }, 0o600);
        this.cookie = cookie;
        this.csrf = data.csrf_token; this.user = data.user; this.loginStatus = 'signed-in';
        return new Response('Signed in to Launchpad. You can close this tab.', { headers: { 'Content-Type': 'text/plain', 'Referrer-Policy': 'no-referrer' } });
      } catch (error) { this.loginStatus = String(error); return new Response('Sign-in failed. Return to Launchpad and try again.', { status: 400 }); }
      finally { setTimeout(() => this.cancelLogin(), 500); }
    }});
    const redirect = `http://127.0.0.1:${listener.port}/callback`;
    this.listener = listener;
    this.timer = setTimeout(() => { this.cancelLogin(); this.loginStatus = 'expired'; }, 300000);
    const url = new URL('/auth/discord', BASE);
    url.search = new URLSearchParams({ redirect_uri: redirect, code_challenge: challenge, code_challenge_method: 'S256', state }).toString();
    return url.href;
  }
  async logout() {
    try { if (this.cookie) await this.json('/api/v1/logout', { method: 'POST' }); }
    finally { await rm(this.sessionFile, { force: true }); this.cancelLogin(); this.clearSession(); }
  }
}
