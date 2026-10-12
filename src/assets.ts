import { join } from 'node:path';
export interface AppAssets { html: string; style: string; chrome: string; logo: string; javascript: string; launchHeader: string; trayMask: Uint8Array }
export async function loadAssets(): Promise<AppAssets> {
  const ui = join(import.meta.dir, '../ui');
  const bundle = await Bun.build({ entrypoints: [join(ui, 'app.ts')], target: 'browser' });
  if (!bundle.success) throw new Error(bundle.logs.join('\n'));
  const [html, style, chrome, logo, javascript, mask, header] = await Promise.all([
    Bun.file(join(ui, 'index.html')).text(), Bun.file(join(ui, 'style.css')).text(),
    Bun.file(join(ui, 'chrome.css')).text(), Bun.file(join(ui, 'logo.svg')).text(),
    bundle.outputs[0]!.text(), Bun.file(join(ui, 'tray-mask.bin')).arrayBuffer(),
    Bun.file(join(ui, 'launch-header.png')).arrayBuffer(),
  ]);
  return { html, style, chrome, logo, javascript, launchHeader: Buffer.from(header).toString('base64'), trayMask: new Uint8Array(mask) };
}
