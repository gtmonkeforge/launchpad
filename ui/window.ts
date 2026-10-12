type Command = 'status' | 'minimize' | 'maximize' | 'close' | 'drag' | 'resize';
declare global {
  interface Window { __launchpadWindow?: (action: Command, edge?: number) => Promise<{ ok: boolean; maximized: boolean; platform: string; error?: string }> }
  interface Window { __launchpadPreferences?: (preferences: { exitToTray: boolean; busy: boolean; accent: string }) => Promise<{ ok: boolean; error?: string }> }
}
export async function syncWindowPreferences(settings: { exitToTray: boolean; accent: string }, busy: boolean) {
  if (!window.__launchpadPreferences) return;
  const result = await window.__launchpadPreferences({ exitToTray: settings.exitToTray === true, accent: settings.accent, busy });
  if (!result.ok) throw new Error(result.error || 'Could not apply desktop preferences');
}
export function setupWindowControls(report: (message: string) => void, canClose: () => boolean) {
  const titlebar = document.querySelector<HTMLElement>('#titlebar')!;
  const minimize = document.querySelector<HTMLButtonElement>('#window-minimize')!;
  const maximize = document.querySelector<HTMLButtonElement>('#window-maximize')!;
  const close = document.querySelector<HTMLButtonElement>('#window-close')!;
  if (!window.__launchpadWindow) {
    for (const button of [minimize, maximize, close]) button.title = 'Available in the desktop app';
    return;
  }
  for (const button of [minimize, maximize, close]) button.disabled = false;
  async function command(action: Command, edge?: number) {
    try {
      const result = await window.__launchpadWindow!(action, edge);
      if (!result.ok) throw new Error(result.error);
      if (result.platform === 'linux' && !document.querySelector('.resize-edge')) {
        ['nw', 'n', 'ne', 'w', 'e', 'sw', 's', 'se'].forEach((direction, edge) => {
          const grip = document.createElement('div'); grip.className = `resize-edge resize-${direction}`; grip.setAttribute('aria-hidden', 'true');
          grip.onmousedown = event => { if (event.button === 0) { event.preventDefault(); void command('resize', edge); } };
          document.body.append(grip);
        });
      }
      maximize.setAttribute('aria-label', result.maximized ? 'Restore' : 'Maximize');
      maximize.title = result.maximized ? 'Restore' : 'Maximize';
      maximize.innerHTML = result.maximized
        ? '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 6h9v9M6 9h9v9H6z"/></svg>'
        : '<svg viewBox="0 0 24 24" aria-hidden="true"><rect x="6" y="6" width="12" height="12"/></svg>';
    } catch (error) { report(error instanceof Error ? error.message : 'Window action failed'); }
  }
  minimize.onclick = () => void command('minimize');
  maximize.onclick = () => void command('maximize');
  close.onclick = () => { if (canClose()) void command('close'); else report('Wait for mod installation to finish before closing Launchpad.'); };
  titlebar.onmousedown = event => {
    if (event.button !== 0 || (event.target as HTMLElement).closest('.window-controls')) return;
    event.preventDefault(); void command(event.detail === 2 ? 'maximize' : 'drag');
  };
  window.addEventListener('resize', () => void command('status'));
  window.addEventListener('focus', () => void command('status'));
  void command('status');
}
