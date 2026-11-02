import { pluginFolders } from './mod-loaders';
import type { ModLoader } from './types';
import { lstat, readdir, readFile } from 'node:fs/promises';
import { join, relative, basename } from 'node:path';
import { createHash } from 'node:crypto';
import { noSymlinks } from './mods';
export interface ShieldCheck { ruleId?: string; title?: string; status: string; explanation?: string }
export interface ShieldReport { status: string; verdict?: string; score?: number; assemblyName?: string; isUnsafe?: boolean; checks: ShieldCheck[]; diagnostics?: string[]; sha256?: string }
export interface ScanFile { path: string; status: 'complete' | 'incomplete' | 'error' | 'skipped'; sha256?: string; report?: ShieldReport; error?: string }
export interface SecurityState { scanning: boolean; folder: string; startedAt?: string; finishedAt?: string; current: string; total: number; files: ScanFile[]; error?: string }
export class PluginSecurity {
  state: SecurityState = { scanning: false, folder: '', current: '', total: 0, files: [] };
  constructor(private readonly scanDll: (bytes: Uint8Array, name: string) => Promise<ShieldReport> = scanAssembly) {}
  reset() { this.state = { scanning: false, folder: '', current: '', total: 0, files: [] }; }
  async scanIfNeeded(game: string, canScan: () => boolean = () => true, loader: ModLoader = 'bepinex5') {
    if (!game || this.state.startedAt || this.state.scanning) return;
    // Wait until plugins exist; selecting a game or installing the loader can create them later.
    const previous = this.state;
    const folders = await Promise.all(pluginFolders(loader).map(folder => lstat(join(game, folder)).catch(() => null)));
    if (this.state !== previous || !folders.some(folder => folder?.isDirectory()) || this.state.startedAt || this.state.scanning || !canScan()) return;
    return this.scan(game, loader);
  }
  async scan(game: string, loader: ModLoader = 'bepinex5') {
    if (this.state.scanning) throw new Error('A MonkeShield scan is already running');
    if (!game) throw new Error('Select your Gorilla Tag game folder in Settings first');
    const roots = pluginFolders(loader).map(folder => join(game, folder));
    const folder = roots.join(' · ');
    this.state = { scanning: true, folder, startedAt: new Date().toISOString(), current: '', total: 0, files: [] };
    const state = this.state;
    try {
      const files: string[] = [];
      const walk = async (dir: string): Promise<void> => {
        for (const entry of (await readdir(dir, { withFileTypes: true })).sort((a, b) => a.name.localeCompare(b.name))) {
          const file = join(dir, entry.name), path = relative(roots.length === 1 ? roots[0]! : game, file);
          if (entry.isSymbolicLink()) { state.files.push({ path, status: 'skipped', error: 'Symbolic link was not followed.' }); continue; }
          if (entry.isDirectory()) await walk(file);
          else if (entry.isFile() && /\.dll$/i.test(entry.name)) files.push(file);
        }
      };
      let found = false;
      for (const root of roots) {
        await noSymlinks(game, root);
        if (!(await lstat(root).catch(() => null))?.isDirectory()) continue;
        found = true; await walk(root);
      }
      if (!found) throw new Error('No plugin folders found for the selected loader');
      state.total = files.length;
      for (const file of files) {
        const path = relative(roots.length === 1 ? roots[0]! : game, file); state.current = path;
        try {
          await noSymlinks(game, file);
          const stat = await lstat(file);
          if (!stat.isFile() || stat.size > 512 * 1024 * 1024) throw new Error('File is not regular or exceeds MonkeShield’s 512 MB limit');
          const bytes = new Uint8Array(await readFile(file));
          const sha256 = createHash('sha256').update(bytes).digest('hex');
          const report = await this.scanDll(bytes, basename(file));
          if (report.sha256 && report.sha256.toLowerCase() !== sha256) throw new Error('Scanner report does not match the uploaded file');
          const after = await lstat(file);
          if (after.size !== stat.size || after.mtimeMs !== stat.mtimeMs || after.ino !== stat.ino) throw new Error('File changed during scanning. Scan again.');
          state.files.push({ path, sha256, status: report.status === 'Complete' && !!report.assemblyName && report.checks.length > 0 && report.checks.every(c => ['Pass', 'Fail', 'Inconclusive'].includes(c.status)) ? 'complete' : 'incomplete', report });
        } catch (error) { state.files.push({ path, status: 'error', error: error instanceof Error ? error.message : String(error) }); }
      }
    } catch (error) { state.error = error instanceof Error ? error.message : String(error); }
    finally { state.scanning = false; state.current = ''; state.finishedAt = new Date().toISOString(); }
    return state;
  }
}
export async function scanAssembly(bytes: Uint8Array, name: string): Promise<ShieldReport> {
  const url = new URL('https://shield.monkeforge.org/api/scan'); url.searchParams.set('name', name);
  const response = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: new Uint8Array(bytes).buffer, redirect: 'error', signal: AbortSignal.timeout(120000) });
  const data = await response.json() as any;
  if (!response.ok && !(response.status === 422 && Array.isArray((data.report || data)?.checks))) throw new Error(data.detail || data.error || `MonkeShield returned ${response.status}`);
  const report = data.report || data;
  if (!report || typeof report.status !== 'string' || !Array.isArray(report.checks) || report.checks.some((c: any) => !c || typeof c.status !== 'string')) throw new Error('MonkeShield returned an invalid report');
  return report;
}
