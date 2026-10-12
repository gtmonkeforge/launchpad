import { dlopen, FFIType as t, JSCallback, ptr, toArrayBuffer, type Pointer } from 'bun:ffi';
import { attemptTrayOperation } from './tray-operation';
export interface NativePreferences { exitToTray: boolean; busy: boolean; accent: string }
export interface TrayController { update(preferences: NativePreferences): void; hide(): void; dispose(): void }
const TRAY_MESSAGE = 0x8001;
/** x64 NOTIFYICONDATAW; strings are UTF-16 and HWND/HICON are pointer-sized. */
export function notificationData(handle: Pointer, icon: Pointer) {
  const bytes = new Uint8Array(976), v = new DataView(bytes.buffer);
  v.setUint32(0, bytes.length, true); v.setBigUint64(8, BigInt(handle), true);
  v.setUint32(16, 1, true); v.setUint32(20, 7, true); v.setUint32(24, TRAY_MESSAGE, true);
  v.setBigUint64(32, BigInt(icon), true);
  bytes.set(Buffer.from('Launchpad\0', 'utf16le'), 40);
  return bytes;
}
export function createWindowsTray(handle: Pointer, alpha: Uint8Array, initial: NativePreferences, report: (message: string) => void): TrayController {
  if (process.platform !== 'win32' || (process.arch !== 'x64' && process.arch !== 'arm64')) throw new Error('Tray support requires 64-bit Windows');
  if (alpha.length !== 32 * 32) throw new Error('Invalid tray icon mask');
  const user = dlopen('user32.dll', {
    GetWindowLongPtrW: { args: [t.ptr, t.i32], returns: t.i64_fast },
    SetWindowLongPtrW: { args: [t.ptr, t.i32, t.i64_fast], returns: t.i64_fast },
    CallWindowProcW: { args: [t.ptr, t.ptr, t.u32, t.u64_fast, t.i64_fast], returns: t.i64_fast },
    ShowWindow: { args: [t.ptr, t.i32], returns: t.i32 }, IsWindow: { args: [t.ptr], returns: t.i32 },
    SetForegroundWindow: { args: [t.ptr], returns: t.i32 },
    PostMessageW: { args: [t.ptr, t.u32, t.u64_fast, t.i64_fast], returns: t.i32 },
    RegisterWindowMessageW: { args: [t.ptr], returns: t.u32 },
    CreatePopupMenu: { args: [], returns: t.ptr },
    AppendMenuW: { args: [t.ptr, t.u32, t.u64_fast, t.ptr], returns: t.i32 },
    TrackPopupMenuEx: { args: [t.ptr, t.u32, t.i32, t.i32, t.ptr, t.ptr], returns: t.u32 },
    DestroyMenu: { args: [t.ptr], returns: t.i32 }, GetCursorPos: { args: [t.ptr], returns: t.i32 },
    CreateIconIndirect: { args: [t.ptr], returns: t.ptr }, DestroyIcon: { args: [t.ptr], returns: t.i32 },
  });
  const shell = dlopen('shell32.dll', { Shell_NotifyIconW: { args: [t.u32, t.ptr], returns: t.i32 } });
  const gdi = dlopen('gdi32.dll', {
    CreateDIBSection: { args: [t.ptr, t.ptr, t.u32, t.ptr, t.ptr, t.u32], returns: t.ptr },
    CreateBitmap: { args: [t.i32, t.i32, t.u32, t.u32, t.ptr], returns: t.ptr },
    DeleteObject: { args: [t.ptr], returns: t.i32 },
  });
  const s = user.symbols;
  let preferences = initial, icon: Pointer | null = null, data: Uint8Array | null = null;
  let added = false, hidden = false, quitting = false, destroyed = false;
  const original = Number(s.GetWindowLongPtrW(handle, -4)) as Pointer;
  if (!original) throw new Error('The native window procedure is unavailable');
  const wide = (text: string) => Buffer.from(text + '\0', 'utf16le');
  const explorerRestarted = s.RegisterWindowMessageW(ptr(wide('TaskbarCreated')));
  function makeIcon(accent: string) {
    const info = new Uint8Array(40), v = new DataView(info.buffer), bits = new BigUint64Array(1);
    v.setUint32(0, 40, true); v.setInt32(4, 32, true); v.setInt32(8, -32, true);
    v.setUint16(12, 1, true); v.setUint16(14, 32, true);
    const colorBitmap = gdi.symbols.CreateDIBSection(null, ptr(info), 0, ptr(bits), null, 0);
    if (!colorBitmap || !bits[0]) throw new Error('Could not create the tray icon bitmap');
    let maskBitmap: Pointer | null = null;
    try {
      const pixels = new Uint8Array(toArrayBuffer(bits[0], 0, 4096));
      const rgb = [1, 3, 5].map(i => parseInt(accent.slice(i, i + 2), 16));
      for (let i = 0; i < alpha.length; i++) {
        const a = alpha[i]!; pixels[i * 4] = Math.round(rgb[2]! * a / 255);
        pixels[i * 4 + 1] = Math.round(rgb[1]! * a / 255); pixels[i * 4 + 2] = Math.round(rgb[0]! * a / 255); pixels[i * 4 + 3] = a;
      }
      const mask = gdi.symbols.CreateBitmap(32, 32, 1, 1, ptr(new Uint8Array(128)));
      maskBitmap = mask ? Number(mask) as Pointer : null;
      if (!maskBitmap) throw new Error('Could not create the tray transparency bitmap');
      const iconInfo = new Uint8Array(32), iv = new DataView(iconInfo.buffer);
      iv.setUint32(0, 1, true); iv.setBigUint64(16, BigInt(maskBitmap), true); iv.setBigUint64(24, BigInt(colorBitmap), true);
      const result = s.CreateIconIndirect(ptr(iconInfo));
      if (!result) throw new Error('Could not create the Launchpad tray icon');
      return Number(result) as Pointer;
    } finally { if (maskBitmap) gdi.symbols.DeleteObject(maskBitmap); gdi.symbols.DeleteObject(colorBitmap); }
  }
  function remove() { if (added && data) shell.symbols.Shell_NotifyIconW(2, ptr(data)); added = false; }
  function ensure() {
    return attemptTrayOperation(() => {
    if (!icon) { icon = makeIcon(preferences.accent); data = notificationData(handle, icon); }
    if (!added) {
      // Explorer may be unavailable; keep Launchpad accessible and retry later.
      if (!shell.symbols.Shell_NotifyIconW(0, ptr(data!))) return false;
      added = true;
    }
    return true;
    }, () => { if (hidden) show(); });
  }
  function show() { if (!destroyed) { s.ShowWindow(handle, 5); s.SetForegroundWindow(handle); hidden = false; } }
  function hide() { if (!ensure()) return; s.ShowWindow(handle, 0); hidden = true; }
  function quit() {
    if (preferences.busy) { show(); report('Wait for mod installation to finish before quitting Launchpad.'); return; }
    quitting = true; s.PostMessageW(handle, 0x10, 0, 0);
  }
  function menu() {
    const menu = s.CreatePopupMenu(); if (!menu) return;
    try {
      s.AppendMenuW(menu, 0, 1, ptr(wide('Open Launchpad')));
      s.AppendMenuW(menu, 0x800, 0, null);
      s.AppendMenuW(menu, preferences.busy ? 1 : 0, 2, ptr(wide('Quit')));
      const point = new Int32Array(2); s.GetCursorPos(ptr(point)); s.SetForegroundWindow(handle);
      const selected = s.TrackPopupMenuEx(menu, 0x102, point[0]!, point[1]!, handle, null);
      if (selected === 1) show(); else if (selected === 2) quit();
      s.PostMessageW(handle, 0, 0, 0);
    } finally { s.DestroyMenu(menu); }
  }
  const callback = new JSCallback((hwnd: Pointer, message: number, wparam: number | bigint, lparam: number | bigint) => {
    try {
      if (message === 0x10 && !quitting) {
        if (preferences.busy) { show(); report('Wait for mod installation to finish before closing Launchpad.'); return 0; }
        if (preferences.exitToTray) { hide(); return 0; }
      }
      if (message === TRAY_MESSAGE) {
        const event = Number(lparam) & 0xffff;
        if (event === 0x202 || event === 0x203 || event === 0x400 || event === 0x401) show();
        else if (event === 0x205 || event === 0x7b) menu();
        return 0;
      }
      if (explorerRestarted && message === explorerRestarted && preferences.exitToTray) { added = false; ensure(); return 0; }
      if (message === 2) { remove(); destroyed = true; }
    } catch (error) { show(); report(error instanceof Error ? error.message : 'Tray operation failed'); return 0; }
    return s.CallWindowProcW(original, hwnd, message, wparam, lparam);
  }, { args: [t.ptr, t.u32, t.u64_fast, t.i64_fast], returns: t.i64_fast });
  s.SetWindowLongPtrW(handle, -4, BigInt(callback.ptr!));
  return {
    update(next) {
      if (typeof next.exitToTray !== 'boolean' || typeof next.busy !== 'boolean' || !/^#[a-f0-9]{6}$/i.test(next.accent)) throw new Error('Invalid native preferences');
      const previousAccent = preferences.accent;
      preferences = { ...next };
      attemptTrayOperation(() => {
      if (next.accent !== previousAccent && icon) {
        const replacement = makeIcon(next.accent), replacementData = notificationData(handle, replacement);
        if (added && !shell.symbols.Shell_NotifyIconW(1, ptr(replacementData))) { s.DestroyIcon(replacement); remove(); return false; }
        s.DestroyIcon(icon); icon = replacement; data = replacementData;
      }
      if (next.exitToTray) ensure(); else { if (hidden) show(); remove(); }
      return true;
      }, () => { if (hidden) show(); });
    },
    hide,
    dispose() {
      remove();
      if (!destroyed && s.IsWindow(handle)) s.SetWindowLongPtrW(handle, -4, Number(original));
      callback.close(); if (icon) s.DestroyIcon(icon);
      gdi.close(); shell.close(); user.close();
    },
  };
}
