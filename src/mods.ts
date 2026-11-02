import { createHash } from 'node:crypto';
import { existsSync } from 'node:fs';
import { lstat, mkdir, readFile, rm, writeFile, rename } from 'node:fs/promises';
import { join, resolve, relative, isAbsolute, dirname, basename } from 'node:path';
import { unzipSync } from 'fflate';
import { atomicJson, readJson } from './config';
import { gameRunning, configureProton } from './steam';
import type { MonkeForge } from './api';
import { loaderNames, loaderProxy, selectDownload, modEntries } from './mod-loaders';
import type { Installed, Mod, Download, ModLoader } from './types';
const LIMIT = 200 * 1024 * 1024;
export function safePath(root: string, path: string) {
  if (!path || path.includes('\0') || path.includes(':') || /(^|[\\/])\.\.([\\/]|$)/.test(path) || /^[\\/]/.test(path)) throw new Error('Unsafe archive path');
  for (const part of path.split(/[\\/]/)) if (/[. ]$/.test(part) || /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i.test(part) || /[<>"|?*]/.test(part)) throw new Error('Unsafe Windows archive filename');
  const target = resolve(root, path.replace(/\\/g, '/'));
  const rel = relative(resolve(root), target);
  if (!rel || rel.startsWith('..') || isAbsolute(rel)) throw new Error('Path escapes installation directory');
  return target;
}
export async function noSymlinks(root: string, target: string) {
  let current = resolve(root);
  for (const part of relative(current, target).split(/[\\/]/)) {
    if ((await lstat(current).catch(() => null))?.isSymbolicLink()) throw new Error('Installation path contains a symbolic link');
    current = join(current, part);
  }
  if ((await lstat(target).catch(() => null))?.isSymbolicLink()) throw new Error('Installation target is a symbolic link');
}
export function archiveEntries(bytes: Uint8Array) {
  let total = 0, count = 0;
  const files = unzipSync(bytes, { filter: entry => {
    total += entry.originalSize; count++;
    if (total > LIMIT || count > 10000) throw new Error('Archive exceeds extraction limits');
    return !entry.name.endsWith('/');
  }});
  for (const name of Object.keys(files)) safePath('/archive', name);
  return files;
}
export class ModManager {
  status = ''; busy = false;
  constructor(private api: MonkeForge, public game: string, private readonly setupProton: (loader: ModLoader) => Promise<unknown> = loader => configureProton(undefined, undefined, loaderProxy(loader))) {}
  async manifest(): Promise<{ installed: Installed[]; [key: string]: unknown }> {
    if (!this.game) return { installed: [] };
    const manifest = await readJson(join(this.game, 'forge.json'), { installed: [] } as { installed: Installed[] });
    if (!Array.isArray(manifest.installed) || manifest.installed.some(m => !m.guid || !Array.isArray(m.files) || !Array.isArray(m.folders))) throw new Error('Invalid forge.json manifest');
    return manifest;
  }
  async locked<T>(action: () => Promise<T>) {
    if (this.busy) throw new Error('Another mod operation is in progress');
    if (!this.game || !existsSync(join(this.game, 'Gorilla Tag.exe'))) throw new Error('Select the Steam Gorilla Tag game folder in Settings first');
    this.busy = true;
    const lock = join(this.game, '.launchpad.lock'); let acquired = false;
    try { if (await gameRunning()) throw new Error('Close Gorilla Tag before changing mods'); await writeFile(lock, String(process.pid), { flag: 'wx' }); acquired = true; return await action(); }
    finally { if (acquired) await rm(lock, { force: true }); this.busy = false; this.status = ''; }
  }
  async download(file: Download) {
    if (file.size > 50 * 1024 * 1024 || file.size < 1 || !/^[a-f0-9]{64}$/i.test(file.sha256)) throw new Error('Invalid download size or checksum');
    const bytes = new Uint8Array(await (await this.api.request(file.url)).arrayBuffer());
    if (bytes.length !== file.size || createHash('sha256').update(bytes).digest('hex') !== file.sha256.toLowerCase()) throw new Error('Download integrity check failed');
    return bytes;
  }
  async transaction(record: Installed, entries: Record<string, Uint8Array>, removeOnly = false, adoptLoader = false) {
    const manifest = await this.manifest();
    const old = manifest.installed.find(m => m.guid === record.guid);
    const targets = new Map<string, Uint8Array>();
    for (const [name, bytes] of Object.entries(entries)) targets.set(safePath(this.game, name), bytes);
    const oldPaths: string[] = [];
    for (const file of old?.files || []) {
      const target = isAbsolute(file) ? resolve(file) : safePath(this.game, file);
      safePath(this.game, relative(this.game, target)); oldPaths.push(target);
    }
    // CLI may record whole directories. Enumerate them so rollback restores every file.
    const { readdir } = await import('node:fs/promises');
    const walk = async (folder: string): Promise<void> => {
      safePath(this.game, relative(this.game, folder)); await noSymlinks(this.game, folder);
      for (const entry of await readdir(folder, { withFileTypes: true }).catch((e: any) => { if (e.code === 'ENOENT') return []; throw e; })) {
        const path = join(folder, entry.name);
        if (entry.isSymbolicLink()) throw new Error('Managed folder contains a symbolic link');
        if (entry.isDirectory()) await walk(path); else oldPaths.push(path);
      }
    };
    for (const folder of old?.folders || []) await walk(isAbsolute(folder) ? resolve(folder) : safePath(this.game, folder));
    const other = manifest.installed.filter(m => m.guid !== record.guid);
    for (const target of new Set([...oldPaths, ...targets.keys()])) {
      await noSymlinks(this.game, target);
      if (other.some(m => m.files.some(f => resolve(this.game, f) === target) || m.folders.some(f => { const r = relative(resolve(this.game, f), target); return r === '' || (!r.startsWith('..') && !isAbsolute(r)); }))) throw new Error(`File is shared with another mod: ${basename(target)}`);
      if (targets.has(target) && existsSync(target) && !oldPaths.includes(target) && !adoptLoader) throw new Error(`Unmanaged file would be overwritten: ${relative(this.game, target)}`);
    }
    const backup = new Map<string, Uint8Array | null>();
    for (const target of new Set([...oldPaths, ...targets.keys()])) backup.set(target, existsSync(target) ? new Uint8Array(await readFile(target)) : null);
    if (adoptLoader) for (const [target, bytes] of backup) {
      if (!bytes) continue;
      const copy = safePath(this.game, `.launchpad/${record.guid}-backup/${relative(this.game, target)}`);
      await noSymlinks(this.game, copy); await mkdir(dirname(copy), { recursive: true });
      await writeFile(copy, bytes, { flag: 'wx' }).catch((e: any) => { if (e.code !== 'EEXIST') throw e; });
    }
    try {
      for (const target of oldPaths) await rm(target, { force: true });
      for (const [target, bytes] of targets) { await mkdir(dirname(target), { recursive: true }); await writeFile(target, bytes); }
      manifest.installed = other;
      if (!removeOnly) manifest.installed.push({ ...record, files: [...targets.keys()], folders: [] });
      await atomicJson(join(this.game, 'forge.json'), manifest);
    } catch (error) {
      for (const [target, bytes] of backup) { if (bytes) { await mkdir(dirname(target), { recursive: true }); await writeFile(target, bytes); } else if (existsSync(target)) await rm(target, { force: true }); }
      throw error;
    }
  }
  async ensureBepInEx() {
    this.status = 'Checking BepInEx…';
    const response = await fetch('https://api.github.com/repos/BepInEx/BepInEx/releases/latest', { headers: { 'User-Agent': 'MonkeForge-Launchpad' }, signal: AbortSignal.timeout(30000) });
    if (!response.ok) throw new Error('Could not check the latest BepInEx release');
    const release = await response.json() as any;
    const version = String(release.tag_name).replace(/^v/, '');
    const installed = (await this.manifest()).installed.find(m => m.guid === 'bepinex');
    if (installed?.version === version && existsSync(join(this.game, 'BepInEx/core/BepInEx.dll')) && (existsSync(join(this.game, 'winhttp.dll')) || existsSync(join(this.game, '.launchpad/disabled-loaders/bepinex5/winhttp.dll')))) return;
    const asset = release.assets.find((a: any) => /^BepInEx_win_x64_.*\.zip$/i.test(a.name));
    if (!asset) throw new Error('Latest BepInEx release has no Windows x64 package');
    if (asset.size > 50 * 1024 * 1024) throw new Error('BepInEx package is too large');
    this.status = 'Installing BepInEx…';
    const download = await fetch(asset.browser_download_url, { signal: AbortSignal.timeout(60000) });
    if (!download.ok) throw new Error('BepInEx download failed');
    const bytes = new Uint8Array(await download.arrayBuffer());
    if (bytes.length !== asset.size) throw new Error('BepInEx download size mismatch');
    if (asset.digest?.startsWith('sha256:') && createHash('sha256').update(bytes).digest('hex') !== asset.digest.slice(7)) throw new Error('BepInEx checksum mismatch');
    const entries = archiveEntries(bytes);
    for (const name of Object.keys(entries)) if (!/^(BepInEx\/|winhttp\.dll$|doorstop_config\.ini$|\.doorstop_version$|changelog\.txt$)/i.test(name.replace(/\\/g, '/'))) throw new Error('Unexpected BepInEx package content');
    await this.transaction({ guid: 'bepinex', name: 'BepInEx', version, files: [], folders: [] }, entries, false, true);
  }
  
  async ensureMelonLoader() {
    this.status = 'Checking MelonLoader…';
    const response = await fetch('https://api.github.com/repos/LavaGang/MelonLoader/releases/latest', { headers: { 'User-Agent': 'MonkeForge-Launchpad' }, signal: AbortSignal.timeout(30000) });
    if (!response.ok) throw new Error('Could not check the latest MelonLoader release');
    const release = await response.json() as any;
    const version = String(release.tag_name).replace(/^v/, '');
    const installed = (await this.manifest()).installed.find(mod => mod.guid === 'melonloader');
    if (installed?.version === version && existsSync(join(this.game, 'MelonLoader/net6/MelonLoader.dll')) && (existsSync(join(this.game, 'version.dll')) || existsSync(join(this.game, '.launchpad/disabled-loaders/melonloader/version.dll')))) return;
    const asset = release.assets.find((asset: any) => asset.name === 'MelonLoader.x64.zip');
    if (!asset || asset.size > 50 * 1024 * 1024) throw new Error('No supported MelonLoader Windows x64 package');
    this.status = 'Installing MelonLoader…';
    const download = await fetch(asset.browser_download_url, { signal: AbortSignal.timeout(60000) });
    if (!download.ok) throw new Error('MelonLoader download failed');
    const bytes = new Uint8Array(await download.arrayBuffer());
    if (bytes.length !== asset.size) throw new Error('MelonLoader download size mismatch');
    if (asset.digest?.startsWith('sha256:') && createHash('sha256').update(bytes).digest('hex') !== asset.digest.slice(7)) throw new Error('MelonLoader checksum mismatch');
    const entries = archiveEntries(bytes);
    for (const name of Object.keys(entries)) if (!/^(MelonLoader\/|version\.dll$|dobby\.dll$|LICENSE(?:\.md|\.txt)?$)/i.test(name.replace(/\\/g, '/'))) throw new Error('Unexpected MelonLoader package content');
    if (!entries['version.dll'] || !entries['MelonLoader/net6/MelonLoader.dll']) throw new Error('MelonLoader package is incomplete');
    await this.transaction({ guid: 'melonloader', name: 'MelonLoader', version, files: [], folders: [] }, entries, false, true);
    await mkdir(join(this.game, 'Mods'), { recursive: true }); await mkdir(join(this.game, 'Plugins'), { recursive: true });
  }
  async activateLoader(loader: ModLoader) {
    const inactive: ModLoader = loader === 'melonloader' ? 'bepinex5' : 'melonloader';
    const activeProxy = join(this.game, loaderProxy(loader) + '.dll');
    const savedProxy = join(this.game, '.launchpad/disabled-loaders', loader, loaderProxy(loader) + '.dll');
    const inactiveProxy = join(this.game, loaderProxy(inactive) + '.dll');
    const disabledProxy = join(this.game, '.launchpad/disabled-loaders', inactive, loaderProxy(inactive) + '.dll');
    const moved: [string, string][] = [];
    const move = async (from: string, to: string) => {
      await noSymlinks(this.game, from); await noSymlinks(this.game, to);
      if (existsSync(to)) { const previous = to + '.previous-' + crypto.randomUUID(); await rename(to, previous); moved.push([to, previous]); }
      await mkdir(dirname(to), { recursive: true }); await rename(from, to); moved.push([from, to]);
    };
    try {
      if (existsSync(activeProxy) && existsSync(savedProxy)) await move(savedProxy, savedProxy + '.previous-' + crypto.randomUUID());
      const otherRuntime = inactive === 'melonloader' ? 'MelonLoader' : 'BepInEx/core/BepInEx.dll';
      if (existsSync(inactiveProxy) && existsSync(join(this.game, otherRuntime))) await move(inactiveProxy, disabledProxy);
      if (!existsSync(activeProxy) && existsSync(savedProxy)) await move(savedProxy, activeProxy);
      if (!existsSync(activeProxy)) throw new Error(`${loaderNames[loader]} bootstrap is missing`);
    } catch (error) { for (const [from, to] of moved.reverse()) await rename(to, from); throw error; }
  }
  async prepareLoader(loader: ModLoader) {
    return this.locked(async () => {
      await this.setupProton(loader);
      const runtime = loader === 'melonloader' ? 'MelonLoader/net6/MelonLoader.dll' : 'BepInEx/core/BepInEx.dll';
      const proxy = loaderProxy(loader) + '.dll';
      if (!existsSync(join(this.game, runtime)) || (!existsSync(join(this.game, proxy)) && !existsSync(join(this.game, '.launchpad/disabled-loaders', loader, proxy)))) {
        if (loader === 'melonloader') await this.ensureMelonLoader(); else await this.ensureBepInEx();
      }
      await this.activateLoader(loader);
    });
  }
  async install(guid: string, channel = 'stable', loader: ModLoader = 'bepinex5') {
    return this.locked(async () => {
      const mod = await this.api.json(`/api/v1/mods/${encodeURIComponent(guid)}?channel=${encodeURIComponent(channel)}`) as Mod;
      if (!/^[a-zA-Z0-9._-]+$/.test(mod.guid)) throw new Error('Invalid mod GUID');
      const release = mod.releases[0];
      if (!release) throw new Error('This channel has no downloadable release');
      const file = selectDownload(release, loader);
      this.status = `Downloading ${mod.name}…`;
      const bytes = await this.download(file);
      const entries = file.kind === 'dll' ? { [`${loader === 'melonloader' ? 'Mods' : 'BepInEx/plugins/' + mod.guid}/${mod.name.replace(/[^a-zA-Z0-9._-]/g, '_')}.dll`]: bytes } : modEntries(archiveEntries(bytes), loader, mod.guid);
      await this.setupProton(loader);
      if (loader === 'melonloader') await this.ensureMelonLoader(); else await this.ensureBepInEx();
      await this.activateLoader(loader);
      this.status = `Installing ${mod.name}…`;
      await this.transaction({ guid: mod.guid, name: mod.name, version: release.version, loader, files: [], folders: [] }, entries);
      return { guid: mod.guid, version: release.version };
    });
  }

  async uninstall(guid: string) {
    return this.locked(async () => {
      if (['bepinex', 'melonloader'].includes(guid)) throw new Error('Mod loaders cannot be removed as mods');
      const record = (await this.manifest()).installed.find(m => m.guid === guid);
      if (!record) throw new Error('Mod is not installed');
      this.status = `Removing ${record.name}…`; await this.transaction(record, {}, true);
    });
  }
}
