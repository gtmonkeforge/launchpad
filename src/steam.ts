import { existsSync } from 'node:fs';
import { readFile, writeFile, readdir } from 'node:fs/promises';
import { homedir } from 'node:os';
import { join } from 'node:path';
export const APP_ID = '1533390';
export function steamRoots() {
  return process.platform === 'win32' ? [join(process.env['PROGRAMFILES(X86)'] || 'C:/Program Files (x86)', 'Steam'), join(process.env.PROGRAMFILES || 'C:/Program Files', 'Steam')] : [join(homedir(), '.steam/steam'), join(homedir(), '.local/share/Steam'), join(homedir(), '.var/app/com.valvesoftware.Steam/.local/share/Steam')];
}
export async function discoverGame() {
  for (const root of steamRoots()) {
    const libraries = [root];
    try { const vdf = await readFile(join(root, 'steamapps/libraryfolders.vdf'), 'utf8'); for (const m of vdf.matchAll(/"path"\s+"([^"]+)"/g)) libraries.push(m[1]!.replace(/\\\\/g, '\\')); } catch {}
    for (const library of libraries) { const path = join(library, 'steamapps/common/Gorilla Tag'); if (existsSync(join(path, 'Gorilla Tag.exe'))) return path; }
  }
  return '';
}
export async function openExternal(url: string) {
  const parsed = new URL(url);
  if (!['https:', 'http:', 'steam:'].includes(parsed.protocol)) throw new Error('Unsupported link');
  if (parsed.protocol === 'steam:' && url !== `steam://rungameid/${APP_ID}`) throw new Error('Unsupported Steam command');
  const args = process.platform === 'win32' ? ['rundll32.exe', 'url.dll,FileProtocolHandler', url] : ['xdg-open', url];
  const child = Bun.spawn(args, { stdout: 'ignore', stderr: 'pipe', windowsHide: true });
  if (await child.exited) throw new Error('Unable to open Steam or your default browser');
}
export async function gameRunning() {
  const args = process.platform === 'win32' ? ['tasklist.exe', '/FO', 'CSV', '/NH'] : ['ps', '-A', '-o', 'comm=,args='];
  const child = Bun.spawn(args, { stdout: 'pipe', stderr: 'ignore', windowsHide: true });
  const result = await new Response(child.stdout).text();
  if (await child.exited) throw new Error('Could not read game process status');
  return /Gorilla Tag\.exe/i.test(result);
}
interface Node { key: string; start: number; end: number; value?: string; children?: Node[]; close?: number }
export function parseVdf(source: string): Node[] {
  const tokens = [...source.matchAll(/"(?:\\.|[^"\\])*"|[{}]|\/\/[^\n]*/g)].filter(t => !t[0].startsWith('//'));
  let i = 0;
  function group(): Node[] {
    const nodes: Node[] = [];
    while (i < tokens.length && tokens[i]![0] !== '}') {
      const key = tokens[i++]!, val = tokens[i++]!;
      if (!key || !val || !key[0].startsWith('"')) throw new Error('Invalid Steam settings');
      const decode = (s: string) => s.slice(1, -1).replace(/\\([\\"])/g, '$1');
      if (val[0] === '{') {
        const children = group(), close = tokens[i++]!;
        if (!close || close[0] !== '}') throw new Error('Invalid Steam settings');
        nodes.push({ key: decode(key[0]), start: key.index!, end: close.index! + 1, children, close: close.index! });
      } else nodes.push({ key: decode(key[0]), start: val.index!, end: val.index! + val[0].length, value: decode(val[0]) });
    }
    return nodes;
  }
  return group();
}
export function wineOptions(options: string, proxy: 'winhttp' | 'version' = 'winhttp') {
  const assignments = [...options.matchAll(/(?:^|\s)WINEDLLOVERRIDES=(?:"([^"\n]*)"|'([^'\n]*)'|([^\s"']+))/g)];
  const assignment = assignments.at(-1);
  if (assignment) {
    const value = assignment[1] ?? assignment[2] ?? assignment[3] ?? '';
    const entries = value.split(';');
    const targetsWinhttp = (entry: string) => entry.split('=')[0]!.split(',').some(name => name.trim().replace(/\.dll$/i, '').toLowerCase() === proxy);
    // Preserve valid launch options byte-for-byte, including quoting and custom ordering.
    const override = entries.filter(targetsWinhttp).at(-1);
    if (override && /^(n|native)$/i.test((override.split('=')[1] || '').split(',')[0]!.trim())) return options;
    const updated = entries.flatMap(entry => {
      if (!targetsWinhttp(entry)) return [entry];
      const [names, modes] = entry.split('=');
      const others = names!.split(',').filter(name => name.trim().replace(/\.dll$/i, '').toLowerCase() !== proxy);
      return others.length ? [others.join(',') + '=' + modes] : [];
    }).filter(Boolean).concat(proxy + '=n,b').join(';');
    const start = assignment.index! + assignment[0].indexOf('WINEDLLOVERRIDES=');
    const end = assignment.index! + assignment[0].length;
    return options.slice(0, start) + `WINEDLLOVERRIDES="${updated}"` + options.slice(end);
  }
  if (/(?:^|\s)WINEDLLOVERRIDES=/.test(options)) throw new Error('Invalid WINEDLLOVERRIDES value in Steam launch options');
  return `WINEDLLOVERRIDES="${proxy}=n,b" ${options.includes('%command%') ? options : `%command%${options ? ' ' + options : ''}`}`;
}
export function patchLaunchOptions(source: string, proxy: 'winhttp' | 'version' = 'winhttp') {
  let nodes = parseVdf(source);
  for (const key of ['UserLocalConfigStore', 'Software', 'Valve', 'Steam', 'apps']) {
    const node = nodes.find(n => n.key.toLowerCase() === key.toLowerCase());
    if (!node?.children) throw new Error('Steam settings not found. Launch the game once, then retry.');
    nodes = node.children;
  }
  const app = nodes.find(n => n.key === APP_ID);
  const quote = (s: string) => `"${s.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"`;
  if (!app?.children || app.close === undefined) throw new Error('Launch Gorilla Tag once, then retry Proton setup.');
  const launch = app.children.find(n => n.key.toLowerCase() === 'launchoptions');
  if (launch) {
    const updated = wineOptions(launch.value || '', proxy);
    if (updated === launch.value) return source;
    return source.slice(0, launch.start) + quote(updated) + source.slice(launch.end);
  }
  return source.slice(0, app.close) + `\n"LaunchOptions" ${quote(wineOptions('', proxy))}\n` + source.slice(app.close);
}
export async function configureProton(roots = steamRoots(), platform: NodeJS.Platform = process.platform, proxy: 'winhttp' | 'version' = 'winhttp') {
  if (platform !== 'linux') return { configured: 0, updated: 0 };
  let count = 0, updated = 0;
  for (const root of roots) {
    const accounts = await readdir(join(root, 'userdata')).catch(() => []);
    for (const account of accounts) {
      const file = join(root, 'userdata', account, 'config/localconfig.vdf');
      if (!existsSync(file)) continue;
      const source = await readFile(file, 'utf8');
      if (!source.includes(`"${APP_ID}"`)) continue;
      const patched = patchLaunchOptions(source, proxy);
      if (patched !== source) { await writeFile(`${file}.launchpad-backup`, source, { flag: 'wx' }).catch(e => { if (e.code !== 'EEXIST') throw e; }); await writeFile(file, patched); updated++; }
      count++;
    }
  }
  if (!count) throw new Error('Launch Gorilla Tag once through Steam, then retry Proton setup.');
  return { configured: count, updated };
}
