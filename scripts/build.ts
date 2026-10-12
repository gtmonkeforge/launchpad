import { join, resolve } from 'node:path';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { existsSync } from 'node:fs';
import { loadAssets } from '../src/assets';
const root = resolve(import.meta.dir, '..');
const output = join(root, 'dist');
const windowsIcon = existsSync(join(root, 'logo.ico')) ? join(root, 'logo.ico') : join(root, 'ui/launchpad.ico');
const names = process.argv.slice(2);
if (names.some(n => n !== 'win64' && n !== 'tux64')) throw new Error('Usage: bun scripts/build.ts [win64|tux64]');
await mkdir(output, { recursive: true });
const assets = await loadAssets();
const assetModule = `const assets = ${JSON.stringify({ ...assets, trayMask: undefined })};\nexport async function loadAssets() { return { ...assets, trayMask: Uint8Array.from(Buffer.from(${JSON.stringify(Buffer.from(assets.trayMask).toString('base64'))}, 'base64')) }; }`;
const targets = [
  { name: 'win64', target: 'bun-windows-x64', filename: 'launchpad-win64.exe', library: 'libwebview.dll' },
  { name: 'tux64', target: 'bun-linux-x64', filename: 'launchpad-tux64', library: 'libwebview-x64.so' },
] as const;
for (const build of targets) {
  if (names.length && !names.includes(build.name)) continue;
  console.log(`Building ${build.filename}…`);
  const library = join(root, 'node_modules/webview-bun/build', build.library).replace(/\\/g, '/');
  const compiled = await Bun.build({
    entrypoints: [join(root, 'index.ts'), join(root, 'src/server.ts')], target: 'bun',
    minify: true, sourcemap: 'linked',
    compile: { target: build.target, outfile: join(output, build.filename),
      ...(build.name === 'win64' ? { windows: { hideConsole: true, icon: windowsIcon, title: 'Launchpad', publisher: 'MonkeForge', description: 'MonkeForge Launchpad' } } : {}) },
    plugins: [{ name: 'launchpad-deployment', setup(builder) {
      builder.onLoad({ filter: /[\\/]src[\\/]assets\.ts$/ }, () => ({ contents: assetModule, loader: 'js' }));
      builder.onLoad({ filter: /[\\/]src[\\/]native\.ts$/ }, () => ({ contents: `
        import embeddedLibrary from ${JSON.stringify(library)} with { type: 'file' };
        import { materializeNativeLibrary } from './deployment';
        export async function loadNativeWebview() {
          if (!process.env.WEBVIEW_PATH) process.env.WEBVIEW_PATH = await materializeNativeLibrary(embeddedLibrary);
          return import('webview-bun');
        }`, loader: 'ts' }));
      builder.onLoad({ filter: /[\\/]webview-bun[\\/]src[\\/]ffi\.ts$/ }, async ({ path }) => {
        const source = await readFile(path, 'utf8');
        const start = source.indexOf('let lib_file;'), end = source.indexOf('export const lib = dlopen');
        if (start < 0 || end <= start) throw new Error('webview-bun native loader changed; update the build adapter');
        return { contents: source.slice(0, start) + 'const lib_file = { default: process.env.WEBVIEW_PATH };\n' + source.slice(end), loader: 'ts' };
      });
    }}],
  });
  if (!compiled.success) throw new Error(compiled.logs.join('\n'));
  const bytes = await readFile(join(output, build.filename));
  const sha = createHash('sha256').update(bytes).digest('hex');
  await writeFile(join(output, build.filename + '.sha256'), `${sha}  ${build.filename}\n`);
  console.log(`${build.filename}: ${(bytes.length / 1024 / 1024).toFixed(1)} MiB · SHA-256 ${sha}`);
}
