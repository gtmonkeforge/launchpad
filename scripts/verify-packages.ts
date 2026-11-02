import { mkdtemp, mkdir, readFile, access, rm, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { createHash } from 'node:crypto';
import { version } from '../package.json';
const dist = resolve(import.meta.dir, '../dist');
const temp = await mkdtemp(join(tmpdir(), 'launchpad-package-check-'));
async function run(args: string[], env?: Record<string, string | undefined>) {
  const proc = Bun.spawn(args, { cwd: temp, env: { ...process.env, ...env }, stdout: 'pipe', stderr: 'pipe' });
  const [out, err, code] = await Promise.all([new Response(proc.stdout).text(), new Response(proc.stderr).text(), proc.exited]);
  if (code) throw new Error(`${args[0]} failed (${code}): ${err || out}`);
  return out;
}
async function absent(path: string) { if (await access(path).then(() => true, () => false)) throw new Error(`Uninstall left ${path}`); }
try {
  for (const name of [`launchpad-${version}-windows-x64-setup.exe`, `launchpad-${version}-linux-x64.tar.gz`]) {
    const bytes = await readFile(join(dist, name));
    const hash = createHash('sha256').update(bytes).digest('hex');
    if (!(await readFile(join(dist, name + '.sha256'), 'utf8')).startsWith(hash + '  ')) throw new Error(`Checksum mismatch: ${name}`);
    if (name.endsWith('.exe') && bytes.toString('ascii', 0, 2) !== 'MZ') throw new Error('Windows installer is not a PE executable');
  }
  if (process.platform !== 'linux') { console.log('Package checksums and Windows executable header verified. Run on Linux to verify install/uninstall.'); process.exit(0); }
  await run(['tar', '-xzf', join(dist, `launchpad-${version}-linux-x64.tar.gz`), '-C', temp]);
  const source = join(temp, `launchpad-${version}-linux-x64`);
  const env = { HOME: join(temp, 'Home with spaces'), XDG_DATA_HOME: join(temp, 'Data 50% $dollar "quote" `tick` \\slash'), XDG_CONFIG_HOME: join(temp, 'config'), XDG_CACHE_HOME: join(temp, 'cache') };
  await mkdir(env.HOME);
  const app = join(env.XDG_DATA_HOME, 'monkeforge-launchpad');
  const desktop = join(env.XDG_DATA_HOME, 'applications/monkeforge-launchpad.desktop');
  const icon = join(env.XDG_DATA_HOME, 'icons/hicolor/scalable/apps/monkeforge-launchpad.svg');
  await run(['bash', join(source, 'install.sh')], env);
  await access(icon);
  if (Bun.which('desktop-file-validate')) await run(['desktop-file-validate', desktop]);
  const expected = await readFile(join(source, 'launchpad-tux64'));
  if (!(await readFile(join(app, 'launchpad-tux64'))).equals(expected)) throw new Error('Installed executable differs from archive');
  const check = JSON.parse(await run([join(app, 'launchpad-tux64'), '--self-check'], env));
  if (!check.ok || !check.standalone) throw new Error('Installed standalone self-check failed');
  const preferences = join(env.XDG_CONFIG_HOME, 'launchpad/settings.json');
  await mkdir(join(env.XDG_CONFIG_HOME, 'launchpad'), { recursive: true });
  await writeFile(preferences, '{"openOnStartup":false}\n');
  await run(['bash', join(source, 'install.sh')], env);
  const startup = join(env.XDG_CONFIG_HOME, 'autostart/monkeforge-launchpad.desktop');
  await mkdir(join(env.XDG_CONFIG_HOME, 'autostart'), { recursive: true });
  await writeFile(startup, '# MonkeForge Launchpad managed startup\n');
  await run(['bash', join(app, 'uninstall.sh')], env);
  for (const path of [app, desktop, icon, startup]) await absent(path);
  if (await readFile(preferences, 'utf8') !== '{"openOnStartup":false}\n') throw new Error('Uninstall modified preferences');
  await run(['bash', join(source, 'uninstall.sh')], env);
  // Unrelated startup files must survive uninstall.
  await writeFile(startup, '# not managed by Launchpad\n');
  await run(['bash', join(source, 'uninstall.sh')], env);
  await access(startup);
  console.log('Verified checksums, Windows PE header, Linux install/upgrade/uninstall, desktop validity, icon, installed standalone self-check, unusual paths, and preserved preferences.');
} finally { await rm(temp, { recursive: true, force: true }); }
