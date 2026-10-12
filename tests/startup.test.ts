import { afterAll, expect, test } from 'bun:test';
import { mkdtemp, readFile, writeFile, rm } from 'node:fs/promises';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { defaults, normalizeSettings } from '../src/config';
import { desktopQuote, registerStartup, startupContent, windowsQuote } from '../src/startup';
const temp = await mkdtemp(join(tmpdir(), 'launchpad-startup-tests-'));
afterAll(() => rm(temp, { recursive: true, force: true }));
test('missing preferences default to enabled while saved choices and Linux tray restrictions are preserved', () => {
  expect(normalizeSettings({ accent: '#abcdef' }, 'win32')).toEqual({ ...defaults, accent: '#abcdef' });
  expect(normalizeSettings({ exitToTray: false, openOnStartup: false }, 'win32')).toEqual({ ...defaults, exitToTray: false, openOnStartup: false });
  expect(normalizeSettings({}, 'linux')).toEqual({ ...defaults, exitToTray: false });
  expect(normalizeSettings({ ...defaults, exitToTray: true, openOnStartup: true }, 'linux')).toEqual({ ...defaults, exitToTray: false, openOnStartup: true });
});
test('Windows startup launches the native app hidden from the console, preserving arguments', () => {
  const content = startupContent('win32', { executable: 'C:\\Program Files\\Bun\\bun.exe', args: ['C:\\My App\\index.ts'] });
  expect(content).toContain('""C:\\Program Files\\Bun\\bun.exe"" ""C:\\My App\\index.ts"" ""--startup""');
  expect(content).toContain(', 0, False');
  expect(content).not.toContain('--browser');
  expect(windowsQuote('C:\\directory\\')).toBe('"C:\\directory\\\\"');
  expect(() => startupContent('win32', { executable: 'C:/%PATH%/app.exe', args: [] })).toThrow();
});
test('Linux startup quotes paths and escapes desktop field codes without a shell', () => {
  const content = startupContent('linux', { executable: '/opt/My App/bun', args: ['/opt/$files/app.ts'] });
  expect(content).toContain('Exec="/opt/My App/bun" "/opt/\\\\$files/app.ts" "--startup"');
  expect(content).toContain('Terminal=false');
  expect(desktopQuote('/tmp/100%')).toBe('"/tmp/100%%"');
});
test('startup registration can be enabled, disabled, and rolled back without touching other files', async () => {
  const file = join(temp, 'startup/launchpad.vbs'), other = join(temp, 'unrelated.txt');
  await writeFile(other, 'keep');
  const launch = { executable: 'C:/Program Files/Launchpad.exe', args: [] };
  const undoEnable = await registerStartup(true, launch, 'win32', file);
  const original = await readFile(file); expect(original.toString('utf16le')).toContain('--startup');
  const undoDisable = await registerStartup(false, launch, 'win32', file);
  expect(await Bun.file(file).exists()).toBe(false);
  await undoDisable(); expect(await readFile(file)).toEqual(original);
  await undoEnable(); expect(await Bun.file(file).exists()).toBe(false);
  expect(await readFile(other, 'utf8')).toBe('keep');
});
test('refuses to overwrite a startup file it does not own', async () => {
  const file = join(temp, 'unowned.vbs'); await writeFile(file, 'other application');
  await expect(registerStartup(true, { executable: 'app.exe', args: [] }, 'win32', file)).rejects.toThrow('unrelated');
  await expect(registerStartup(false, { executable: 'app.exe', args: [] }, 'win32', file)).rejects.toThrow('unrelated');
  expect(await readFile(file, 'utf8')).toBe('other application');
});
test('Windows registration preserves Unicode paths with a UTF-16 BOM', async () => {
  const file = join(temp, 'unicode.vbs');
  await registerStartup(true, { executable: 'C:/Users/José/Launchpad.exe', args: [] }, 'win32', file);
  const bytes = await readFile(file);
  expect([...bytes.subarray(0, 2)]).toEqual([255, 254]);
  expect(bytes.toString('utf16le')).toContain('C:/Users/José/Launchpad.exe');
});
