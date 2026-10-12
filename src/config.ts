import { homedir } from 'node:os';
import { join } from 'node:path';
import { mkdir, readFile, rename, writeFile } from 'node:fs/promises';
export const configDir = process.platform === 'win32' ? process.env.APPDATA || join(homedir(), 'AppData/Roaming') : process.env.XDG_CONFIG_HOME || join(homedir(), '.config');
export const settingsFile = join(configDir, 'launchpad/settings.json');
export const FRIENDS_URL = 'https://friends.monkeforge.org';
export interface Settings { accent: string; channels: Record<string, string>; exitToTray: boolean; openOnStartup: boolean }
export const defaults: Settings = { accent: '#dc5c3b', channels: {}, exitToTray: true, openOnStartup: true };
export function normalizeSettings(saved: Partial<Settings>, platform = process.platform): Settings {
  return { accent: saved.accent || defaults.accent, channels: saved.channels || {}, exitToTray: platform === 'win32' && (saved.exitToTray ?? defaults.exitToTray) === true, openOnStartup: (saved.openOnStartup ?? defaults.openOnStartup) === true };
}
export async function readJson<T>(file: string, fallback: T): Promise<T> {
  try { return JSON.parse(await readFile(file, 'utf8')); } catch (error: any) { if (error.code === 'ENOENT') return fallback; throw new Error(`Could not read ${file}. Repair the file before continuing.`); }
}
export async function atomicJson(file: string, value: unknown, mode?: number) {
  await mkdir(join(file, '..'), { recursive: true });
  const temp = `${file}.${crypto.randomUUID()}.tmp`;
  await writeFile(temp, JSON.stringify(value, null, 2), { mode }); await rename(temp, file);
}
