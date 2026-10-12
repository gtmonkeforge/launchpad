import { expect, test } from 'bun:test';
import { MonkeForge } from '../src/api';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

test('native login saves its cookie and a new client restores verified session details', async () => {
  const root = await mkdtemp(join(tmpdir(), 'launchpad-auth-')), file = join(root, 'session.json');
  const realFetch = globalThis.fetch, api = new MonkeForge(file);
  globalThis.fetch = (async (input: any, options?: RequestInit) => {
    const url = String(input);
    if (url.startsWith('http://127.0.0.1:')) return realFetch(input, options);
    if (url.endsWith('/auth/native/exchange')) return new Response(JSON.stringify({ user: { id: 'one' }, csrf_token: 'initial' }), { headers: { 'Set-Cookie': 'session=secret; HttpOnly; Secure' } });
    expect(new Headers(options?.headers).get('Cookie')).toBe('session=secret');
    return Response.json({ user: { id: 'one', name: 'Updated' }, csrf_token: 'fresh' });
  }) as unknown as typeof fetch;
  try {
    const login = new URL(api.startLogin()), callback = new URL(login.searchParams.get('redirect_uri')!);
    callback.search = new URLSearchParams({ code: 'a'.repeat(43), state: login.searchParams.get('state')! }).toString();
    expect((await realFetch(callback)).status).toBe(200);
    expect(JSON.parse(await readFile(file, 'utf8'))).toEqual({ cookie: 'session=secret' });
    const restarted = new MonkeForge(file); await restarted.restoreSession();
    expect(restarted.user?.name).toBe('Updated'); expect(restarted.csrf).toBe('fresh');
    expect(restarted.loginStatus).toBe('signed-in');
  } finally { api.cancelLogin(); globalThis.fetch = realFetch; await rm(root, { recursive: true, force: true }); }
});

test('expired sessions are removed, but connection failures preserve saved credentials', async () => {
  const root = await mkdtemp(join(tmpdir(), 'launchpad-auth-')), file = join(root, 'session.json');
  const realFetch = globalThis.fetch;
  try {
    await writeFile(file, JSON.stringify({ cookie: 'session=secret' }));
    globalThis.fetch = (async () => { throw new Error('Offline'); }) as unknown as typeof fetch;
    const offline = new MonkeForge(file); await offline.restoreSession();
    expect(offline.user).toBeNull(); expect(await readFile(file, 'utf8')).toContain('session=secret');
    globalThis.fetch = (async () => Response.json({ user: null, csrf_token: 'anonymous' })) as unknown as typeof fetch;
    await new MonkeForge(file).restoreSession();
    expect(await Bun.file(file).exists()).toBe(false);
    await writeFile(file, JSON.stringify({ cookie: 'session=secret' }));
    globalThis.fetch = (async () => Response.json({}, { status: 401 })) as unknown as typeof fetch;
    await new MonkeForge(file).restoreSession();
    expect(await Bun.file(file).exists()).toBe(false);
  } finally { globalThis.fetch = realFetch; await rm(root, { recursive: true, force: true }); }
});

test('sign-out clears saved credentials even if remote logout fails', async () => {
  const root = await mkdtemp(join(tmpdir(), 'launchpad-auth-')), file = join(root, 'session.json');
  const realFetch = globalThis.fetch, api = new MonkeForge(file);
  try {
    await writeFile(file, JSON.stringify({ cookie: 'session=secret' }));
    api.cookie = 'session=secret'; api.user = { id: 'one' }; api.csrf = 'token';
    globalThis.fetch = (async () => { throw new Error('Offline'); }) as unknown as typeof fetch;
    await expect(api.logout()).rejects.toThrow('Offline');
    expect(await Bun.file(file).exists()).toBe(false);
    expect(api.cookie).toBe(''); expect(api.user).toBeNull(); expect(api.csrf).toBe('');
  } finally { globalThis.fetch = realFetch; await rm(root, { recursive: true, force: true }); }
});
test('native login rejects mismatched state, duplicate parameters, and wrong callback paths', async () => {
  const api = new MonkeForge(); const login = new URL(api.startLogin());
  try {
    const callback = new URL(login.searchParams.get('redirect_uri')!);
    const state = login.searchParams.get('state')!;
    expect(login.searchParams.get('code_challenge_method')).toBe('S256');
    expect(login.searchParams.get('code_challenge')).toHaveLength(43);
    callback.search = new URLSearchParams({ code: 'a'.repeat(43), state: 'b'.repeat(43) }).toString();
    expect((await fetch(callback)).status).toBe(400);
    callback.search = new URLSearchParams({ code: 'a'.repeat(43), state }).toString(); callback.searchParams.append('state', state);
    expect((await fetch(callback)).status).toBe(400);
    callback.pathname = '/unexpected'; expect((await fetch(callback)).status).toBe(400);
    expect(api.user).toBeNull(); expect(api.cookie).toBe('');
  } finally { api.cancelLogin(); }
});
