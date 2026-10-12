import { createHash } from 'node:crypto';
import { homedir } from 'node:os';
import { join } from 'node:path';
import { lstat, mkdir, readFile, rename, rm, writeFile } from 'node:fs/promises';
/** OS loaders need a real file; Bun's embedded asset paths live in a virtual FS. */
export async function materializeNativeLibrary(embeddedPath: string, cacheRoot?: string) {
  const bytes = new Uint8Array(await Bun.file(embeddedPath).arrayBuffer());
  const hash = createHash('sha256').update(bytes).digest('hex');
  const cache = cacheRoot || (process.platform === 'win32'
    ? process.env.LOCALAPPDATA || join(homedir(), 'AppData/Local')
    : process.env.XDG_CACHE_HOME || join(homedir(), '.cache'));
  const directory = join(cache, 'MonkeForge/Launchpad/native');
  await mkdir(directory, { recursive: true, mode: 0o700 });
  const file = join(directory, `webview-${hash}${process.platform === 'win32' ? '.dll' : '.so'}`);
  const existing = await lstat(file).catch((e: any) => { if (e.code === 'ENOENT') return null; throw e; });
  if (existing) {
    if (!existing.isFile() || existing.isSymbolicLink()) throw new Error('Invalid native library cache entry');
    if (createHash('sha256').update(await readFile(file)).digest('hex') !== hash) throw new Error('Native library cache checksum mismatch');
    return file;
  }
  const temp = `${file}.${crypto.randomUUID()}.tmp`;
  try {
    await writeFile(temp, bytes, { flag: 'wx', mode: 0o600 });
    try { await rename(temp, file); }
    catch (error) {
      // Another instance may have extracted and loaded the same DLL meanwhile.
      const concurrent = await lstat(file).catch(() => null);
      if (!concurrent?.isFile() || concurrent.isSymbolicLink() || createHash('sha256').update(await readFile(file)).digest('hex') !== hash) throw error;
    }
  }
  finally { await rm(temp, { force: true }); }
  return file;
}
