import { expect, test } from 'bun:test';
import { createTrayBehavior } from '../src/tray-behavior';
const initial = { exitToTray: true, busy: false, accent: '#dc5c3b' };
function setup() {
  const calls = { shown: 0, hidden: 0, closed: 0, reported: 0, available: true };
  const tray = createTrayBehavior(initial, { available: () => calls.available, show: () => calls.shown++, hide: () => calls.hidden++, close: () => calls.closed++, report: () => calls.reported++ });
  return { tray, calls };
}
test('close hides only with a reachable tray, and disabling tray restores the hidden window', () => {
  const { tray, calls } = setup();
  expect(tray.closeRequested()).toBe(true); expect(calls.hidden).toBe(1);
  tray.update({ ...initial, exitToTray: false }); expect(calls.shown).toBe(1); expect(tray.closeRequested()).toBe(false);
  tray.update(initial); calls.available = false;
  expect(tray.closeRequested()).toBe(false); expect(tray.hide()).toBe(false); expect(calls.hidden).toBe(1);
});
test('losing the host restores a hidden window and Open restores it again', () => {
  const { tray, calls } = setup(); tray.hide(); tray.unavailable(); expect(calls.shown).toBe(1);
  tray.unavailable(); expect(calls.shown).toBe(1);
  tray.hide(); tray.show(); expect(calls.shown).toBe(2);
});
test('busy operations block close and Quit; explicit Quit bypasses hiding when idle', () => {
  const { tray, calls } = setup(); tray.update({ ...initial, busy: true });
  expect(tray.closeRequested()).toBe(true); tray.quit(); expect(calls.reported).toBe(2); expect(calls.closed).toBe(0); expect(calls.hidden).toBe(0);
  tray.update(initial); tray.quit(); expect(calls.closed).toBe(1); expect(tray.closeRequested()).toBe(false);
});
