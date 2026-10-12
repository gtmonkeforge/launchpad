import { test, expect, afterAll } from 'bun:test';
import { mkdtemp, mkdir, readFile, writeFile, rm, symlink } from 'node:fs/promises';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { zipSync } from 'fflate';
import { archiveEntries, ModManager, safePath } from '../src/mods';
import { MonkeForge } from '../src/api';
import { parseVdf, patchLaunchOptions, wineOptions } from '../src/steam';
const temp = await mkdtemp(join(tmpdir(), 'launchpad-tests-'));
afterAll(() => rm(temp, { recursive: true, force: true }));
test('rejects archive traversal and Windows alternate streams', () => {
  for (const path of ['../outside', 'a/../../outside', '/absolute', 'C:/outside', 'file:stream', '..\\outside', '.. /outside', 'CON.dll', 'trailing.']) expect(() => safePath(temp, path)).toThrow();
  expect(() => archiveEntries(zipSync({ '../outside.dll': new Uint8Array([1]) }))).toThrow();
});
test('extracts valid mod archive', () => {
  const entries = archiveEntries(zipSync({ 'BepInEx/plugins/mod.dll': new Uint8Array([1, 2, 3]) }));
  expect(entries['BepInEx/plugins/mod.dll']).toEqual(new Uint8Array([1, 2, 3]));
});
test('preserves custom Proton arguments and Wine overrides', () => {
  expect(wineOptions('PROTON_LOG=1 %command% -vr')).toBe('WINEDLLOVERRIDES="winhttp=n,b" PROTON_LOG=1 %command% -vr');
  expect(wineOptions('WINEDLLOVERRIDES="dinput8=n,b;winhttp=b" %command%')).toBe('WINEDLLOVERRIDES="dinput8=n,b;winhttp=n,b" %command%');
  expect(wineOptions(wineOptions(''))).toBe(wineOptions(''));
});
test('patches only the Gorilla Tag app in Steam VDF', () => {
  const source = '"UserLocalConfigStore" { "Software" { "Valve" { "Steam" { "apps" { "999" { "LaunchOptions" "untouched" } "1533390" { "LaunchOptions" "-vr" } } } } } }';
  const patched = patchLaunchOptions(source);
  expect(patched).toContain('"999" { "LaunchOptions" "untouched" }');
  expect(patched).toContain('winhttp=n,b');
  expect(patched).toContain('%command% -vr');
  expect(patchLaunchOptions(patched)).toBe(patched);
  expect(parseVdf(patched)).toHaveLength(1);
});
test('updates CLI folder records and keeps unrelated manifest data', async () => {
  const root = join(temp, 'game'); const folder = join(root, 'BepInEx/plugins/example');
  await mkdir(folder, { recursive: true }); await writeFile(join(folder, 'old.dll'), 'old');
  await writeFile(join(root, 'forge.json'), JSON.stringify({ extra: true, installed: [{ guid: 'example', name: 'Example', version: '1', files: [], folders: [folder] }] }));
  const manager = new ModManager(new MonkeForge(), root);
  await manager.transaction({ guid: 'example', name: 'Example', version: '2', files: [], folders: [] }, { 'BepInEx/plugins/example/new.dll': new Uint8Array([42]) });
  const manifest = await manager.manifest(); expect(manifest.extra).toBe(true); expect(manifest.installed[0]!.version).toBe('2');
  expect(await readFile(join(folder, 'new.dll'))).toEqual(Buffer.from([42]));
  expect(await Bun.file(join(folder, 'old.dll')).exists()).toBe(false);
});
test('protects unmanaged files and other mods from overwrites', async () => {
  const root = join(temp, 'conflict'); await mkdir(root); await writeFile(join(root, 'important.dll'), 'keep');
  const manager = new ModManager(new MonkeForge(), root);
  await expect(manager.transaction({ guid: 'new', name: 'New', version: '1', files: [], folders: [] }, { 'important.dll': new Uint8Array([1]) })).rejects.toThrow('Unmanaged');
  expect(await readFile(join(root, 'important.dll'), 'utf8')).toBe('keep');
});
test('rolls back changed files when a later write fails', async () => {
  const root = join(temp, 'rollback'); await mkdir(root); await writeFile(join(root, 'old.dll'), 'original'); await writeFile(join(root, 'blocker'), 'not a directory');
  await writeFile(join(root, 'forge.json'), JSON.stringify({ installed: [{ guid: 'example', name: 'Example', version: '1', files: [join(root, 'old.dll')], folders: [] }] }));
  const manager = new ModManager(new MonkeForge(), root);
  await expect(manager.transaction({ guid: 'example', name: 'Example', version: '2', files: [], folders: [] }, { 'new.dll': new Uint8Array([1]), 'blocker/file.dll': new Uint8Array([2]) })).rejects.toThrow();
  expect(await readFile(join(root, 'old.dll'), 'utf8')).toBe('original'); expect((await manager.manifest()).installed[0]!.version).toBe('1');
  expect(await Bun.file(join(root, 'new.dll')).exists()).toBe(false);
});
test('verifies download checksum and size', async () => {
  const api = new MonkeForge(); api.request = async () => new Response(new Uint8Array([1, 2, 3]));
  const manager = new ModManager(api, temp);
  await expect(manager.download({ filename: 'a.dll', size: 3, sha256: '0'.repeat(64), kind: 'dll', url: 'https://monkeforge.org/api/v1/downloads/a' })).rejects.toThrow('integrity');
});
test('adopts an existing loader with a persistent backup', async () => {
  const root = join(temp, 'loader'); await mkdir(root); await writeFile(join(root, 'winhttp.dll'), 'original loader');
  const manager = new ModManager(new MonkeForge(), root);
  await manager.transaction({ guid: 'bepinex', name: 'BepInEx', version: '5', files: [], folders: [] }, { 'winhttp.dll': new Uint8Array([42]) }, false, true);
  expect(await readFile(join(root, '.launchpad/bepinex-backup/winhttp.dll'), 'utf8')).toBe('original loader');
  expect((await manager.manifest()).installed[0]!.guid).toBe('bepinex');
});
