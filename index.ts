import { loadAssets } from './src/assets';
import { loadNativeWebview } from './src/native';
import { atomicJson, normalizeSettings, readJson, settingsFile, defaults, type Settings } from './src/config';
import { registerStartup } from './src/startup';
const launchCommand = { executable: process.execPath, args: Bun.isStandaloneExecutable ? [] : [Bun.main] };
// Apply the startup default on the first native run, preserving explicit saved choices.
if (!process.argv.includes('--browser') && !process.argv.includes('--self-check')) {
  const saved = await readJson<Partial<Settings>>(settingsFile, {});
  if (saved.openOnStartup === undefined) {
    const preferences = normalizeSettings(saved);
    const rollback = await registerStartup(preferences.openOnStartup, launchCommand);
    try { await atomicJson(settingsFile, preferences); } catch (error) { await rollback(); throw error; }
  }
}
const worker = new Worker(new URL('./src/server.ts', import.meta.url).href);
worker.postMessage({ launchCommand });
worker.onerror = event => { console.error('Launchpad could not start:', event.message); process.exit(1); };
worker.onmessage = async ({ data }) => {
  if (data.error) { console.error(data.error); process.exit(1); }
  if (process.argv.includes('--browser')) { console.log(`Launchpad: ${data.url}`); return; }
  const { Webview } = await loadNativeWebview();
  if (process.argv.includes('--self-check')) {
    const assets = await loadAssets();
    const paths = ['/', '/app.js', '/style.css', '/chrome.css', '/logo.svg', '/launch-header.png'];
    for (const path of paths) {
      const response = await fetch(new URL(path, data.url));
      if (!response.ok || !(await response.text()).length) throw new Error(`Embedded asset check failed: ${path}`);
    }
    if (typeof Webview !== 'function' || assets.trayMask.length !== 1024 || !assets.javascript || !assets.html.includes('Launchpad')) throw new Error('Deployment self-check failed');
    console.log(JSON.stringify({ ok: true, standalone: Bun.isStandaloneExecutable, platform: process.platform, arch: process.arch, worker: true, assets: paths, nativeLibrary: true, startupArgs: launchCommand.args }));
    worker.terminate(); process.exit(0);
  }
  const preferences = normalizeSettings(await readJson(settingsFile, defaults));
  const view = new Webview(false, { width: 1280, height: 860, hint: 0 });
  view.title = 'Launchpad';
  const { createWindowControls } = await import('./src/window');
  const controls = createWindowControls(view.unsafeWindowHandle);
  let tray: import('./src/tray').TrayController | null = null;
  if (process.platform === 'win32') {
    const { createWindowsTray } = await import('./src/tray');
    const alpha = (await loadAssets()).trayMask;
    tray = createWindowsTray(view.unsafeWindowHandle, alpha, { exitToTray: false, busy: false, accent: preferences.accent }, message => {
      view.eval(`window.dispatchEvent(new CustomEvent('launchpad-native-error', {detail:${JSON.stringify(message)}}))`);
    });
    try {
      tray.update({ exitToTray: preferences.exitToTray, busy: false, accent: preferences.accent });
      if (process.argv.includes('--startup') && preferences.exitToTray) tray.hide();
    } catch (error) { console.error(error); }
  }
  view.bind('__launchpadPreferences', (next: import('./src/tray').NativePreferences) => {
    try { tray?.update(next); return { ok: true }; }
    catch (error) { return { ok: false, error: error instanceof Error ? error.message : 'Could not update tray settings' }; }
  });
  view.bind('__launchpadWindow', (action: import('./src/window').WindowAction, edge?: number) => {
    try { return { ok: true, platform: process.platform, ...controls.perform(action, edge) }; }
    catch (error) { return { ok: false, error: error instanceof Error ? error.message : 'Window action failed' }; }
  });
  view.navigate(data.url);
  view.run();
  tray?.dispose();
  controls.dispose();
  worker.terminate();
  process.exit(0);
};
