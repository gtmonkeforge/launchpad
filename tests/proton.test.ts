import { expect, test, spyOn } from 'bun:test';
import { mkdtemp, mkdir, writeFile, readFile, rm, stat, access } from 'node:fs/promises';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { wineOptions, patchLaunchOptions, configureProton } from '../src/steam';
const vdf = (options: string) => `"UserLocalConfigStore" { "Software" { "Valve" { "Steam" { "apps" { "1533390" { "LaunchOptions" ${JSON.stringify(options)} } } } } } }`;
test('preserves already configured overrides exactly, including quoting and custom arguments', () => {
  for (const options of [
    'WINEDLLOVERRIDES="version=n;winhttp=n,b" PROTON_LOG=1 %command% -vr',
    "env WINEDLLOVERRIDES='winhttp=n' %command%",
    'WINEDLLOVERRIDES=winhttp=n,b %command%',
    'WINEDLLOVERRIDES="WINHTTP.dll=native,builtin;dinput8=b" %command%',
    'WINEDLLOVERRIDES="winhttp,version=n,b" %command%',
  ]) { expect(wineOptions(options)).toBe(options); expect(patchLaunchOptions(vdf(options))).toBe(vdf(options)); }
});
test('adds native winhttp without changing unrelated overrides or game arguments', () => {
  expect(wineOptions("WINEDLLOVERRIDES='version,winhttp=b;dinput8=n,b' %command% -vr")).toBe('WINEDLLOVERRIDES="version=b;dinput8=n,b;winhttp=n,b" %command% -vr');
  expect(wineOptions('WINEDLLOVERRIDES=winhttp=b %command%')).toBe('WINEDLLOVERRIDES="winhttp=n,b" %command%');
  expect(() => wineOptions('WINEDLLOVERRIDES="unterminated %command%')).toThrow();
});
test('Proton configuration never checks or stops Steam, and does not write already configured files', async () => {
  const root = await mkdtemp(join(tmpdir(), 'launchpad-proton-'));
  const dir = join(root, 'userdata/123/config'), file = join(dir, 'localconfig.vdf');
  await mkdir(dir, { recursive: true });
  const source = vdf('WINEDLLOVERRIDES=winhttp=n,b %command% -vr'); await writeFile(file, source);
  const before = await stat(file);
  const spawn = spyOn(Bun, 'spawn').mockImplementation(() => { throw new Error('Must not query or stop Steam'); });
  try {
    expect(await configureProton([root], 'linux')).toEqual({ configured: 1, updated: 0 });
    expect(await readFile(file, 'utf8')).toBe(source); expect((await stat(file)).mtimeMs).toBe(before.mtimeMs);
    expect(await access(file + '.launchpad-backup').then(() => true, () => false)).toBe(false);
    await writeFile(file, vdf('PROTON_LOG=1 %command% -vr'));
    expect(await configureProton([root], 'linux')).toEqual({ configured: 1, updated: 1 });
    expect(await readFile(file, 'utf8')).toContain('winhttp=n,b'); expect(await readFile(file + '.launchpad-backup', 'utf8')).toBe(vdf('PROTON_LOG=1 %command% -vr'));
    expect(await configureProton([root], 'linux')).toEqual({ configured: 1, updated: 0 });
    expect(spawn).not.toHaveBeenCalled();
  } finally { spawn.mockRestore(); await rm(root, { recursive: true, force: true }); }
});
test('MelonLoader adds a native version override and preserves the BepInEx override', () => {
  const options = 'WINEDLLOVERRIDES="winhttp=n,b;dinput8=n" %command% -vr';
  const updated = wineOptions(options, 'version');
  expect(updated).toBe('WINEDLLOVERRIDES="winhttp=n,b;dinput8=n;version=n,b" %command% -vr');
  expect(wineOptions(updated, 'version')).toBe(updated);
  expect(patchLaunchOptions(vdf(options), 'version')).toContain('version=n,b');
});
