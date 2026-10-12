import { copyFile, mkdtemp, readFile, rm } from 'node:fs/promises';
import { basename, dirname, join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
const dist = resolve(import.meta.dir, '../dist');
const windows = await readFile(join(dist, 'launchpad-win64.exe'));
const linux = await readFile(join(dist, 'launchpad-tux64'));
if (windows.toString('ascii', 0, 2) !== 'MZ' || windows.readUInt16LE(windows.readUInt32LE(0x3c) + 4) !== 0x8664) throw new Error('Windows artifact is not an x64 PE executable');
if (linux.toString('hex', 0, 4) !== '7f454c46' || linux[4] !== 2 || linux.readUInt16LE(18) !== 62) throw new Error('Linux artifact is not an x64 ELF executable');
console.log('Verified x64 PE and ELF artifact headers.');
const name = process.platform === 'win32' ? 'launchpad-win64.exe' : process.platform === 'linux' ? 'launchpad-tux64' : null;
if (!name) throw new Error('Run deployment verification on Windows or Linux');
const temp = await mkdtemp(join(tmpdir(), 'launchpad-deployment-'));
try {
  const executable = join(temp, name);
  await copyFile(join(dist, name), executable);
  if (process.platform === 'linux') await import('node:fs/promises').then(fs => fs.chmod(executable, 0o755));
  const child = Bun.spawn([executable, '--self-check'], { cwd: temp, stdout: 'pipe', stderr: 'pipe', env: {
    ...process.env, APPDATA: join(temp, 'config'), XDG_CONFIG_HOME: join(temp, 'config'), LOCALAPPDATA: join(temp, 'cache'), XDG_CACHE_HOME: join(temp, 'cache'), WEBVIEW_PATH: '', LAUNCHPAD_PORT: '0',
  }});
  const timeout = setTimeout(() => child.kill(), 30000);
  try {
    const [stdout, stderr, status] = await Promise.all([new Response(child.stdout).text(), new Response(child.stderr).text(), child.exited]);
    if (status !== 0) throw new Error(`Standalone self-check failed (${status}): ${stderr || stdout}`);
    const result = JSON.parse(stdout.trim());
    if (!result.ok || !result.standalone || !result.nativeLibrary || !result.worker || result.startupArgs.length) throw new Error('Incomplete deployment self-check');
    console.log(`${name}: isolated standalone self-check passed (no source tree or node_modules).`);
  } finally { clearTimeout(timeout); }
} finally {
  if (dirname(resolve(temp)) !== resolve(tmpdir()) || !basename(temp).startsWith('launchpad-deployment-')) throw new Error('Refusing cleanup outside the isolated deployment directory');
  await rm(temp, { recursive: true, force: true });
}
