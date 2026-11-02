import { expect, test } from 'bun:test';
import { mkdtemp, mkdir, writeFile, rm, symlink, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { PluginSecurity, scanAssembly, type ShieldReport } from '../src/security';
const good = (): ShieldReport => ({ status: 'Complete', assemblyName: 'Example', verdict: 'BelowThreshold', score: 0, checks: [{ status: 'Pass', title: 'Example check' }] });
async function fixture(action: (game: string, plugins: string) => Promise<void>) {
  const game = await mkdtemp(join(tmpdir(), 'launchpad-security-'));
  const plugins = join(game, 'BepInEx/plugins'); await mkdir(join(plugins, 'nested'), { recursive: true });
  try { await action(game, plugins); } finally { await rm(game, { recursive: true, force: true }); }
}
test('scans all nested plugin DLLs, including unmanaged files; skips other files and links; preserves bytes', () => fixture(async (game, plugins) => {
  await writeFile(join(plugins, 'manual.dll'), 'manual'); await writeFile(join(plugins, 'nested/MOD.DLL'), 'nested'); await writeFile(join(plugins, 'config.txt'), 'config');
  await writeFile(join(game, 'outside.dll'), 'outside'); await symlink(join(game, 'outside.dll'), join(plugins, 'linked.dll'));
  const uploads: string[] = [];
  const security = new PluginSecurity(async bytes => { uploads.push(new TextDecoder().decode(bytes)); return good(); });
  await security.scan(game);
  expect(uploads.sort()).toEqual(['manual', 'nested']); expect(security.state.total).toBe(2);
  expect(security.state.files.filter(f => f.status === 'complete')).toHaveLength(2);
  expect(security.state.files.find(f => f.path === 'linked.dll')?.status).toBe('skipped');
  expect(await readFile(join(plugins, 'manual.dll'), 'utf8')).toBe('manual'); expect(security.state.scanning).toBe(false);
}));
test('continues after upload failures and distinguishes incomplete analyses from completed results', () => fixture(async (game, plugins) => {
  for (const name of ['a.dll', 'b.dll', 'c.dll']) await writeFile(join(plugins, name), name);
  const security = new PluginSecurity(async (_bytes, name) => {
    if (name === 'a.dll') throw new Error('Service unavailable');
    if (name === 'b.dll') return { ...good(), status: 'Incomplete', checks: [{ status: 'NotEvaluated' }] };
    return good();
  });
  const scan = await security.scan(game);
  expect(scan.files.map(f => f.status)).toEqual(['error', 'incomplete', 'complete']); expect(scan.finishedAt).toBeDefined();
}));
test('prevents overlapping scans and marks missing folders and changed assemblies as errors', () => fixture(async (game, plugins) => {
  await writeFile(join(plugins, 'a.dll'), 'original');
  let release!: () => void; const gate = new Promise<void>(resolve => release = resolve);
  const security = new PluginSecurity(async () => { await gate; await writeFile(join(plugins, 'a.dll'), 'changed-size'); return good(); });
  const pending = security.scan(game); await expect(security.scan(game)).rejects.toThrow('already running'); release(); await pending;
  expect(security.state.files[0]?.error).toContain('changed');
  await security.scan(join(game, 'missing')); expect(security.state.error).toBeDefined(); expect(security.state.scanning).toBe(false);
}));
test('sends raw DLL bytes to MonkeShield and handles wrapped reports and HTTP failures', async () => {
  const original = globalThis.fetch;
  globalThis.fetch = (async (input: any, options: any) => {
    const url = new URL(String(input)); expect(url.origin).toBe('https://shield.monkeforge.org'); expect(url.pathname).toBe('/api/scan'); expect(url.searchParams.get('name')).toBe('test & name.dll');
    expect(options.method).toBe('POST'); expect(options.headers['Content-Type']).toBe('application/octet-stream'); expect([...new Uint8Array(options.body)]).toEqual([1, 2, 3]);
    return Response.json({ report: good() });
  }) as unknown as typeof fetch;
  try {
    expect((await scanAssembly(new Uint8Array([1, 2, 3]), 'test & name.dll')).status).toBe('Complete');
    globalThis.fetch = (async () => Response.json({ detail: 'Rate limited' }, { status: 429 })) as unknown as typeof fetch;
    await expect(scanAssembly(new Uint8Array(), 'x.dll')).rejects.toThrow('Rate limited');
    globalThis.fetch = (async () => Response.json({ ...good(), status: 'InvalidInput', assemblyName: null }, { status: 422 })) as unknown as typeof fetch;
    expect((await scanAssembly(new Uint8Array(), 'native.dll')).status).toBe('InvalidInput');
    globalThis.fetch = (async () => Response.json({ status: 'Complete' })) as unknown as typeof fetch;
    await expect(scanAssembly(new Uint8Array(), 'x.dll')).rejects.toThrow('invalid report');
  } finally { globalThis.fetch = original; }
});
test('automatically scans once when plugins exist and waits for a configured folder', () => fixture(async (game, plugins) => {
  await writeFile(join(plugins, 'auto.dll'), 'auto');
  let uploads = 0;
  const security = new PluginSecurity(async () => { uploads++; return good(); });
  await security.scanIfNeeded(''); await security.scanIfNeeded(join(game, 'missing'));
  expect(security.state.startedAt).toBeUndefined();
  await security.scanIfNeeded(game, () => false); expect(uploads).toBe(0);
  await Promise.all([security.scanIfNeeded(game), security.scanIfNeeded(game)]);
  await security.scanIfNeeded(game);
  expect(uploads).toBe(1);
  security.reset(); await security.scanIfNeeded(game); expect(uploads).toBe(2);
}));
test('MelonLoader scans Mods and Plugins recursively, without scanning BepInEx files', () => fixture(async (game, plugins) => {
  await writeFile(join(plugins, 'bep.dll'), 'bep');
  await mkdir(join(game, 'Mods/nested'), { recursive: true }); await mkdir(join(game, 'Plugins'), { recursive: true });
  await writeFile(join(game, 'Mods/nested/melon.dll'), 'melon'); await writeFile(join(game, 'Plugins/plugin.dll'), 'plugin');
  const uploaded: string[] = [];
  const security = new PluginSecurity(async bytes => { uploaded.push(new TextDecoder().decode(bytes)); return good(); });
  await security.scan(game, 'melonloader');
  expect(uploaded.sort()).toEqual(['melon', 'plugin']); expect(security.state.total).toBe(2);
  expect(security.state.files.map(file => file.path.replaceAll('\\', '/'))).toEqual(['Mods/nested/melon.dll', 'Plugins/plugin.dll']);
}));
