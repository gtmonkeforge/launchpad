import { expect, test } from 'bun:test';
import { mkdtemp, mkdir, writeFile, readFile, rm, access } from 'node:fs/promises';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { createHash } from 'node:crypto';
import { zipSync } from 'fflate';
import { selectDownload, modEntries, installedModLoader } from '../src/mod-loaders';
import { normalizeSettings } from '../src/config';
import { ModManager } from '../src/mods';
import type { MonkeForge } from '../src/api';
import type { Download, Mod, ModLoader, Release } from '../src/types';
const dll = new Uint8Array([1, 2, 3]);
const file = (loader: ModLoader): Download => ({ loader, filename: loader + '.dll', kind: 'dll', url: '/' + loader, size: dll.length, sha256: createHash('sha256').update(dll).digest('hex') });
const release = { version: '1.0', notes: '', released_at: '', downloads: [file('bepinex5'), file('melonloader')] } satisfies Release;
test('settings default to BepInEx 5 and retain the selected loader', () => {
  expect(normalizeSettings({}).loader).toBe('bepinex5');
  expect(normalizeSettings({ loader: 'melonloader' }).loader).toBe('melonloader');
});
test('selects only downloads compatible with the chosen loader', () => {
  expect(selectDownload(release, 'melonloader').url).toBe('/melonloader');
  expect(selectDownload(release, 'bepinex5').url).toBe('/bepinex5');
  expect(() => selectDownload({ ...release, downloads: [file('bepinex5')] }, 'melonloader')).toThrow('no MelonLoader download');
});
test('routes MelonLoader DLL archives to Mods and rejects cross-loader/runtime writes', () => {
  expect(Object.keys(modEntries({ 'Example.dll': dll }, 'melonloader', 'example'))).toEqual(['Mods/Example.dll']);
  expect(Object.keys(modEntries({ 'Mods/Example.dll': dll, 'Plugins/Helper.dll': dll }, 'melonloader', 'example'))).toEqual(['Mods/Example.dll', 'Plugins/Helper.dll']);
  expect(() => modEntries({ 'BepInEx/plugins/Example.dll': dll }, 'melonloader', 'example')).toThrow();
  expect(() => modEntries({ 'Mods/Example.dll': dll }, 'bepinex5', 'example')).toThrow();
  expect(() => modEntries({ 'Mods/Example.dll': dll, 'version.dll': dll }, 'melonloader', 'example')).toThrow();
  expect(installedModLoader({ guid: 'old', name: 'Old', version: '1', files: ['BepInEx/plugins/Old.dll'], folders: [] })).toBe('bepinex5');
  expect(installedModLoader({ guid: 'old', name: 'Old', version: '1', files: [], folders: ['Mods/Example'] })).toBe('melonloader');
});
test('installs the MelonLoader runtime and matching mod, and switches bootstraps without deleting mods', async () => {
  const game = await mkdtemp(join(tmpdir(), 'launchpad-melon-'));
  const original = globalThis.fetch;
  const loaderZip = zipSync({ 'version.dll': dll, 'MelonLoader/net6/MelonLoader.dll': dll });
  const requested: string[] = [], configured: ModLoader[] = [];
  const api = { json: async () => ({ guid: 'example', name: 'Example', releases: [release] } as Mod), request: async (url: string) => { requested.push(url); return new Response(dll); } } as unknown as MonkeForge;
  class TestManager extends ModManager { override async locked<T>(action: () => Promise<T>) { return action(); } }
  const mods = new TestManager(api, game, async loader => { configured.push(loader); });
  globalThis.fetch = (async (url: any) => String(url).includes('/releases/latest') ? Response.json({ tag_name: 'v0.7.3', assets: [{ name: 'MelonLoader.x64.zip', size: loaderZip.length, browser_download_url: 'https://example.invalid/melon.zip', digest: 'sha256:' + createHash('sha256').update(loaderZip).digest('hex') }] }) : new Response(loaderZip)) as unknown as typeof fetch;
  try {
    await mkdir(join(game, 'BepInEx/core'), { recursive: true });
    await writeFile(join(game, 'BepInEx/core/BepInEx.dll'), dll); await writeFile(join(game, 'winhttp.dll'), 'bep-bootstrap');
    await writeFile(join(game, 'BepInEx/old-mod.dll'), 'keep');
    await mods.install('example', 'stable', 'melonloader');
    expect(requested).toEqual(['/melonloader']); expect(configured).toEqual(['melonloader']);
    expect(await readFile(join(game, 'Mods/Example.dll'))).toEqual(Buffer.from(dll));
    expect((await mods.manifest()).installed.find(mod => mod.guid === 'example')?.loader).toBe('melonloader');
    expect(await access(join(game, 'winhttp.dll')).then(() => true, () => false)).toBe(false);
    await access(join(game, 'version.dll'));
    await mods.prepareLoader('bepinex5');
    expect(await readFile(join(game, 'winhttp.dll'), 'utf8')).toBe('bep-bootstrap');
    expect(await readFile(join(game, 'BepInEx/old-mod.dll'), 'utf8')).toBe('keep');
    await access(join(game, 'Mods/Example.dll'));
    expect(await access(join(game, 'version.dll')).then(() => true, () => false)).toBe(false);
    await mods.prepareLoader('melonloader'); await access(join(game, 'version.dll'));
  } finally { globalThis.fetch = original; await rm(game, { recursive: true, force: true }); }
});
