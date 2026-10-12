import { mkdir, readFile, rename, rm, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { configDir } from './config';
export interface LaunchCommand { executable: string; args: string[] }
const MARKER = 'MonkeForge Launchpad managed startup';
export function windowsQuote(arg: string): string {
  if (/[\r\n\0]/.test(arg)) throw new Error('Invalid startup argument');
  return '"' + arg.replace(/(\\*)"/g, '$1$1\\"').replace(/(\\+)$/g, '$1$1') + '"';
}
export function desktopQuote(arg: string): string {
  if (/[\r\n\0]/.test(arg)) throw new Error('Invalid startup argument');
  return '"' + arg.replace(/%/g, '%%').replace(/["`$\\]/g, ch => '\\' + ch).replace(/\\/g, '\\\\') + '"';
}
export function startupContent(platform: string, launch: LaunchCommand) {
  const args = [launch.executable, ...launch.args, '--startup'];
  if (platform === 'win32') {
    if (args.some(a => a.includes('%'))) throw new Error('Windows startup paths cannot contain percent signs');
    const command = args.map(windowsQuote).join(' ').replace(/"/g, '""');
    return `' ${MARKER}\r\nCreateObject("WScript.Shell").Run "${command}", 0, False\r\n`;
  }
  if (platform === 'linux') {
    if (launch.executable.includes('=')) throw new Error('The startup executable path cannot contain an equal sign');
    return `# ${MARKER}\n[Desktop Entry]\nType=Application\nName=Launchpad\nExec=${args.map(desktopQuote).join(' ')}\nTerminal=false\nX-GNOME-Autostart-enabled=true\n`;
  }
  throw new Error('Startup is supported on Windows and Linux');
}
export function startupPath(platform = process.platform) {
  return platform === 'win32'
    ? join(configDir, 'Microsoft/Windows/Start Menu/Programs/Startup/MonkeForge Launchpad.vbs')
    : join(configDir, 'autostart/monkeforge-launchpad.desktop');
}
async function atomicData(file: string, content: string | Uint8Array) {
  await mkdir(dirname(file), { recursive: true });
  const temp = `${file}.${crypto.randomUUID()}.tmp`;
  try { await writeFile(temp, content); await rename(temp, file); }
  finally { await rm(temp, { force: true }); }
}
/** Return a rollback function so OS registration and settings stay consistent. */
export async function registerStartup(enabled: boolean, launch: LaunchCommand, platform: string = process.platform, file = startupPath(platform as NodeJS.Platform)) {
  const previous = await readFile(file).catch((e: any) => { if (e.code === 'ENOENT') return null; throw e; });
  const previousText = previous?.toString(previous[0] === 255 && previous[1] === 254 ? 'utf16le' : 'utf8');
  if (previousText !== undefined && !previousText.includes(MARKER)) throw new Error('An unrelated startup entry already uses Launchpad’s filename');
  if (enabled) {
    const content = startupContent(platform, launch);
    // Windows Script Host requires a UTF-16 BOM for non-ASCII executable paths.
    const bytes = platform === 'win32' ? Buffer.concat([Buffer.from([255, 254]), Buffer.from(content, 'utf16le')]) : Buffer.from(content);
    await atomicData(file, bytes);
  } else await rm(file, { force: true });
  return async () => { if (previous === null) await rm(file, { force: true }); else await atomicData(file, previous); };
}
