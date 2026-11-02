import { copyFile, mkdir, mkdtemp, readFile, rm, writeFile, chmod } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { createHash } from 'node:crypto';
import { version } from '../package.json';
const root = resolve(import.meta.dir, '..');
const dist = join(root, 'dist');
const requested = process.argv.slice(2);
if (requested.some(target => !['win64', 'tux64'].includes(target))) throw new Error('Usage: bun scripts/package.ts [win64|tux64]');
const targets = requested.length ? requested : ['win64', 'tux64'];
await mkdir(dist, { recursive: true });
async function run(args: string[], cwd: string) {
  const child = Bun.spawn(args, { cwd, stdout: 'inherit', stderr: 'inherit' });
  if (await child.exited !== 0) throw new Error(`Packaging failed: ${args[0]}`);
}
async function checksum(filename: string) {
  const bytes = await readFile(join(dist, filename));
  await writeFile(join(dist, filename + '.sha256'), `${createHash('sha256').update(bytes).digest('hex')}  ${filename}\n`);
  console.log(`Packaged ${filename} (${(bytes.length / 1024 / 1024).toFixed(1)} MiB)`);
}
if (targets.includes('win64')) {
  const compiler = process.env.MAKENSIS || Bun.which('makensis');
  if (!compiler) throw new Error('Install NSIS 3 and put makensis on PATH, or set MAKENSIS to its executable path.');
  const flag = process.platform === 'win32' ? '/' : '-';
  await run([compiler, `${flag}V2`, `${flag}DAPP_VERSION=${version}`, join(root, 'packaging/windows.nsi')], root);
  await checksum(`launchpad-${version}-windows-x64-setup.exe`);
}
if (targets.includes('tux64')) {
  const staging = await mkdtemp(join(tmpdir(), 'launchpad-package-'));
  try {
    const name = `launchpad-${version}-linux-x64`;
    const folder = join(staging, name);
    await mkdir(folder);
    await copyFile(join(dist, 'launchpad-tux64'), join(folder, 'launchpad-tux64'));
    await copyFile(join(root, 'LICENSE.md'), join(folder, 'LICENSE.md'));
    for (const file of ['install.sh', 'uninstall.sh', 'README.txt']) await copyFile(join(root, 'packaging/linux', file), join(folder, file));
    const logo = await readFile(join(root, 'ui/logo.svg'), 'utf8');
    await writeFile(join(folder, 'monkeforge-launchpad.svg'), logo.replace('fill="#ffffff"', 'fill="#dc5c3b"'));
    for (const file of ['install.sh', 'uninstall.sh', 'launchpad-tux64']) await chmod(join(folder, file), 0o755);
    const filename = name + '.tar.gz';
    await run(['tar', '-czf', join(dist, filename), '-C', staging, name], root);
    await checksum(filename);
  } finally { await rm(staging, { recursive: true, force: true }); }
}
