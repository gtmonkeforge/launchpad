import { dlopen, FFIType as t, JSCallback, ptr } from 'bun:ffi';
import { join, resolve } from 'node:path';
import { createLinuxTray, createStatusNotifier } from '../src/linux-tray';
if (process.platform !== 'linux') throw new Error('Run tray protocol verification on Linux');
const daemon = process.env.DBUS_DAEMON || Bun.which('dbus-daemon');
if (!daemon) throw new Error('Install dbus-daemon or set DBUS_DAEMON');
// Run on a private bus so verification never adds an icon to the user's desktop.
if (!process.argv.includes('--child')) {
  const started = Bun.spawnSync([daemon, '--session', '--fork', '--print-address=1', '--print-pid=1']);
  if (started.exitCode) throw new Error(started.stderr.toString());
  const [address, daemonPid] = started.stdout.toString().trim().split('\n');
  const root = resolve(import.meta.dir, '..');
  const display = `:${1000 + process.pid % 10000}`;
  const env = { ...process.env, DBUS_SESSION_BUS_ADDRESS: address, ...(process.env.VERIFY_GTK_TRAY === '1' ? { GDK_BACKEND: 'broadway', BROADWAY_DISPLAY: display } : {}) };
  const broadway = process.env.VERIFY_GTK_TRAY === '1' ? Bun.spawn(['gtk4-broadwayd', '-a', '127.0.0.1', '-p', '0', display], { stdout: 'ignore', stderr: 'inherit' }) : null;
  if (broadway) await Bun.sleep(100);
  let watcher: ReturnType<typeof Bun.spawn> | undefined;
  try {
    watcher = Bun.spawn(['/usr/bin/python3', join(root, 'tests/fixtures/tray-watcher.py')], { env, stdout: 'pipe', stderr: 'inherit' });
    const reader = (watcher.stdout as ReadableStream<Uint8Array>).getReader();
    const first = await reader.read(); reader.releaseLock();
    if (!first.value || !new TextDecoder().decode(first.value).includes('ready')) throw new Error('Mock watcher failed to start');
    const child = Bun.spawn([process.execPath, import.meta.path, '--child'], { env, stdout: 'inherit', stderr: 'inherit' });
    const timeout = setTimeout(() => child.kill(), 15000);
    try { if (await child.exited !== 0) throw new Error('Tray protocol checks failed'); } finally { clearTimeout(timeout); }
  } finally { watcher?.kill(); broadway?.kill(); process.kill(Number(daemonPid)); }
  process.exit(0);
}
const glib = dlopen('libglib-2.0.so.0', { g_main_context_iteration: { args: [t.ptr, t.i32], returns: t.i32 } });
const timer = setInterval(() => { while (glib.symbols.g_main_context_iteration(null, 0)) {} }, 5);
let shown = 0, quit = 0, unavailable = 0;
const preferences = { exitToTray: true, busy: false, accent: '#dc5c3b' };
const tray = createStatusNotifier(new Uint8Array(1024).fill(255), preferences, { show: () => shown++, quit: () => quit++, unavailable: () => unavailable++ });
async function call(dest: string, path: string, method: string, ...args: string[]) {
  const child = Bun.spawn(['gdbus', 'call', '--session', '--dest', dest, '--object-path', path, '--timeout', '3', '--method', method, '--', ...args], { stdout: 'pipe', stderr: 'pipe' });
  const [out, err, status] = await Promise.all([new Response(child.stdout).text(), new Response(child.stderr).text(), child.exited]);
  if (status) throw new Error(err); return out;
}
function assert(value: unknown, message: string): asserts value { if (!value) throw new Error(message); }
try {
  assert(tray.ensure(), 'Tray did not register');
  const item = (await call('org.kde.StatusNotifierWatcher', '/StatusNotifierWatcher', 'org.kde.StatusNotifierWatcher.GetItem')).match(/'([^']+)'/)?.[1];
  assert(item, 'No registered tray item');
  const properties = await call(item, '/StatusNotifierItem', 'org.freedesktop.DBus.Properties.GetAll', 'org.kde.StatusNotifierItem');
  assert(properties.includes('IconPixmap') && properties.includes('Active'), 'Tray properties unavailable');
  const layout = await call(item, '/MenuBar', 'com.canonical.dbusmenu.GetLayout', '0', '-1', '[]');
  assert(layout.includes('Open Launchpad') && layout.includes('Quit'), 'Tray menu missing');
  await call(item, '/MenuBar', 'com.canonical.dbusmenu.GetGroupProperties', '[1,2]', '[]');
  await call(item, '/MenuBar', 'com.canonical.dbusmenu.GetProperty', '1', 'label');
  await call(item, '/StatusNotifierItem', 'org.kde.StatusNotifierItem.Activate', '0', '0');
  assert(shown === 1, 'Activate did not show window');
  await call(item, '/MenuBar', 'com.canonical.dbusmenu.Event', '2', 'clicked', '<0>', '0');
  assert(quit === 1, 'Quit event not delivered');
  tray.update({ ...preferences, busy: true, accent: '#77b595' });
  assert((await call(item, '/MenuBar', 'com.canonical.dbusmenu.GetLayout', '0', '-1', '[]')).includes("'enabled': <false>"), 'Quit was not disabled');
  await call('org.kde.StatusNotifierWatcher', '/StatusNotifierWatcher', 'org.kde.StatusNotifierWatcher.SetHost', 'false');
  for (let i = 0; i < 50 && tray.ensure(); i++) await Bun.sleep(10);
  assert(!tray.ensure() && unavailable > 0, 'Tray loss did not restore access');
  await call('org.kde.StatusNotifierWatcher', '/StatusNotifierWatcher', 'org.kde.StatusNotifierWatcher.SetHost', 'true');
  for (let i = 0; i < 50 && !tray.ensure(); i++) await Bun.sleep(10);
  assert(tray.ensure(), 'Tray did not recover');
  tray.update({ ...preferences, exitToTray: false });
  assert((await call(item, '/StatusNotifierItem', 'org.freedesktop.DBus.Properties.Get', 'org.kde.StatusNotifierItem', 'Status')).includes('Passive'), 'Disabled tray still active');
  console.log('Linux tray verified on a private D-Bus: icon, menu, activate, quit, busy state, host loss, recovery, and disable.');
} finally { tray.dispose(); clearInterval(timer); glib.close(); }

if (process.env.VERIFY_GTK_TRAY === '1') {
  const gtk = dlopen('libgtk-4.so.1', {
    gtk_init: { args: [], returns: t.void }, gtk_window_new: { args: [], returns: t.ptr },
    gtk_window_present: { args: [t.ptr], returns: t.void }, gtk_window_close: { args: [t.ptr], returns: t.void },
    gtk_widget_get_visible: { args: [t.ptr], returns: t.i32 },
  });
  const loop = dlopen('libglib-2.0.so.0', { g_main_context_iteration: { args: [t.ptr, t.i32], returns: t.i32 } });
  const tick = setInterval(() => { while (loop.symbols.g_main_context_iteration(null, 0)) {} }, 5);
  gtk.symbols.gtk_init();
  const handle = Number(gtk.symbols.gtk_window_new()) as import('bun:ffi').Pointer;
  gtk.symbols.gtk_window_present(handle);
  const object = dlopen('libgobject-2.0.so.0', { g_signal_connect_data: { args: [t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.u32], returns: t.u64 } });
  let destroyed = false;
  const destroy = new JSCallback(() => { destroyed = true; }, { args: [t.ptr, t.ptr], returns: t.void });
  object.symbols.g_signal_connect_data(handle, ptr(Buffer.from('destroy\0')), destroy.ptr, null, null, 0);
  const native = createLinuxTray(handle, new Uint8Array(1024).fill(255), preferences, () => {});
  try {
    gtk.symbols.gtk_window_close(handle);
    assert(!gtk.symbols.gtk_widget_get_visible(handle), 'GTK close did not hide window');
    native.update({ ...preferences, exitToTray: false });
    assert(gtk.symbols.gtk_widget_get_visible(handle), 'Disabling tray did not restore GTK window');
    native.update({ ...preferences, busy: true });
    gtk.symbols.gtk_window_close(handle);
    assert(gtk.symbols.gtk_widget_get_visible(handle), 'Busy operation allowed window to hide');
    native.update(preferences); gtk.symbols.gtk_window_close(handle);
    assert(!gtk.symbols.gtk_widget_get_visible(handle), 'Second close did not hide window');
    await call('org.kde.StatusNotifierWatcher', '/StatusNotifierWatcher', 'org.kde.StatusNotifierWatcher.SetHost', 'false');
    for (let i = 0; i < 50 && !gtk.symbols.gtk_widget_get_visible(handle); i++) await Bun.sleep(10);
    assert(gtk.symbols.gtk_widget_get_visible(handle), 'Host loss left GTK window hidden');
    await call('org.kde.StatusNotifierWatcher', '/StatusNotifierWatcher', 'org.kde.StatusNotifierWatcher.SetHost', 'true');
    await Bun.sleep(30); native.hide();
    assert(!gtk.symbols.gtk_widget_get_visible(handle), 'Window was not hidden before Quit');
    const item = (await call('org.kde.StatusNotifierWatcher', '/StatusNotifierWatcher', 'org.kde.StatusNotifierWatcher.GetItem')).match(/'([^']+)'/)?.[1];
    assert(item, 'Native window tray was not registered');
    await call(item, '/MenuBar', 'com.canonical.dbusmenu.Event', '2', 'clicked', '<0>', '0');
    assert(destroyed, 'Tray Quit left a retained window alive instead of quitting');
    console.log('GTK close-request verified headlessly: hide, restore, busy protection, host loss, and actual window destruction on Quit.');
  } finally { native.dispose(); destroy.close(); object.close(); clearInterval(tick); loop.close(); gtk.close(); }
}

if (process.env.VERIFY_GTK_TRAY === '1') {
  const moduleName = 'webview-bun';
  const { Webview } = await import(moduleName);
  const view = new Webview(false, { width: 240, height: 160, hint: 0 });
  view.setHTML('<!doctype html><title>Launchpad quit regression test</title>');
  const native = createLinuxTray(view.unsafeWindowHandle, new Uint8Array(1024).fill(255), preferences, () => {});
  native.hide();
  // A separate process invokes Quit while the native webview event loop owns this thread.
  const click = Bun.spawn(['/usr/bin/python3', '-c', `
import subprocess, time, re
time.sleep(.2)
item=subprocess.check_output(['gdbus','call','--session','--dest','org.kde.StatusNotifierWatcher','--object-path','/StatusNotifierWatcher','--method','org.kde.StatusNotifierWatcher.GetItem'],text=True)
name=re.search("'([^']+)'",item).group(1)
subprocess.run(['gdbus','call','--session','--dest',name,'--object-path','/MenuBar','--method','com.canonical.dbusmenu.Event','2','clicked','<0>','0'],check=True,stdout=subprocess.DEVNULL)
`], { stdout: 'ignore', stderr: 'inherit' });
  try { view.run(); if (await click.exited !== 0) throw new Error('Quit invocation failed'); }
  finally { native.dispose(); }
  console.log('Hidden Webview.run() exits successfully after tray Quit.');
}
