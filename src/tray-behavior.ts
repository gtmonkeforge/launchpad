import type { NativePreferences } from './tray';
/** Window policy shared by native callbacks and tests; hiding requires a reachable tray. */
export function createTrayBehavior(initial: NativePreferences, native: { available(): boolean; show(): void; hide(): void; close(): void; report(message: string): void }) {
  let preferences = initial, hidden = false, quitting = false;
  const show = () => { native.show(); hidden = false; };
  const hide = () => { if (!native.available()) return false; native.hide(); hidden = true; return true; };
  function blocked() { if (!preferences.busy) return false; show(); native.report('Wait for the current operation to finish before quitting Launchpad.'); return true; }
  return {
    show, hide,
    closeRequested() { if (quitting) return false; if (blocked()) return true; return preferences.exitToTray && hide(); },
    quit() { if (blocked()) return; quitting = true; native.close(); },
    unavailable() { if (hidden) show(); },
    update(next: NativePreferences) {
      if (typeof next.exitToTray !== 'boolean' || typeof next.busy !== 'boolean' || !/^#[a-f0-9]{6}$/i.test(next.accent)) throw new Error('Invalid native preferences');
      preferences = { ...next }; if (!next.exitToTray && hidden) show();
    },
  };
}
