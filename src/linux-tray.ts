import { dlopen, FFIType as t, JSCallback, ptr, CString, type Pointer } from 'bun:ffi';
import { createTrayBehavior } from './tray-behavior';
import type { NativePreferences, TrayController } from './tray';
const ITEM = '/StatusNotifierItem', MENU = '/MenuBar';
const itemXml = `<node><interface name="org.kde.StatusNotifierItem">
<property name="Category" type="s" access="read"/><property name="Id" type="s" access="read"/><property name="Title" type="s" access="read"/><property name="Status" type="s" access="read"/><property name="WindowId" type="u" access="read"/><property name="IconName" type="s" access="read"/><property name="IconPixmap" type="a(iiay)" access="read"/><property name="OverlayIconName" type="s" access="read"/><property name="OverlayIconPixmap" type="a(iiay)" access="read"/><property name="AttentionIconName" type="s" access="read"/><property name="AttentionIconPixmap" type="a(iiay)" access="read"/><property name="AttentionMovieName" type="s" access="read"/><property name="ToolTip" type="(sa(iiay)ss)" access="read"/><property name="ItemIsMenu" type="b" access="read"/><property name="Menu" type="o" access="read"/>
<method name="Activate"><arg type="i" direction="in"/><arg type="i" direction="in"/></method><method name="SecondaryActivate"><arg type="i" direction="in"/><arg type="i" direction="in"/></method><method name="ContextMenu"><arg type="i" direction="in"/><arg type="i" direction="in"/></method><method name="Scroll"><arg type="i" direction="in"/><arg type="s" direction="in"/></method>
<signal name="NewIcon"/><signal name="NewStatus"><arg type="s"/></signal></interface></node>`;
const menuXml = `<node><interface name="com.canonical.dbusmenu">
<property name="Version" type="u" access="read"/><property name="TextDirection" type="s" access="read"/><property name="Status" type="s" access="read"/><property name="IconThemePath" type="as" access="read"/>
<method name="GetLayout"><arg type="i" direction="in"/><arg type="i" direction="in"/><arg type="as" direction="in"/><arg type="u" direction="out"/><arg type="(ia{sv}av)" direction="out"/></method>
<method name="GetGroupProperties"><arg type="ai" direction="in"/><arg type="as" direction="in"/><arg type="a(ia{sv})" direction="out"/></method>
<method name="GetProperty"><arg type="i" direction="in"/><arg type="s" direction="in"/><arg type="v" direction="out"/></method>
<method name="Event"><arg type="i" direction="in"/><arg type="s" direction="in"/><arg type="v" direction="in"/><arg type="u" direction="in"/></method>
<method name="EventGroup"><arg type="a(isvu)" direction="in"/><arg type="ai" direction="out"/></method>
<method name="AboutToShow"><arg type="i" direction="in"/><arg type="b" direction="out"/></method>
<method name="AboutToShowGroup"><arg type="ai" direction="in"/><arg type="ai" direction="out"/><arg type="ai" direction="out"/></method>
<signal name="LayoutUpdated"><arg type="u"/><arg type="i"/></signal></interface></node>`;
export function trayPixels(alpha: Uint8Array, accent: string) {
  if (alpha.length !== 1024 || !/^#[a-f0-9]{6}$/i.test(accent)) throw new Error('Invalid tray icon');
  const rgb = [1, 3, 5].map(i => parseInt(accent.slice(i, i + 2), 16));
  return Array.from(alpha).flatMap(a => [a, ...rgb]); // StatusNotifier ARGB32, network byte order.
}
export interface LinuxTrayActions { show(): void; quit(): void; unavailable(): void }
/** Exports SNI and dbusmenu directly through GIO; no GTK 3/AppIndicator dependency. */
export function createStatusNotifier(alpha: Uint8Array, initial: NativePreferences, actions: LinuxTrayActions) {
  const gio = dlopen('libgio-2.0.so.0', {
    g_dbus_connection_set_exit_on_close: { args: [t.ptr, t.i32], returns: t.void },
    g_dbus_connection_is_closed: { args: [t.ptr], returns: t.i32 },
    g_bus_get_sync: { args: [t.i32, t.ptr, t.ptr], returns: t.ptr },
    g_dbus_node_info_new_for_xml: { args: [t.ptr, t.ptr], returns: t.ptr },
    g_dbus_node_info_lookup_interface: { args: [t.ptr, t.ptr], returns: t.ptr },
    g_dbus_node_info_unref: { args: [t.ptr], returns: t.void },
    g_dbus_connection_register_object: { args: [t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.ptr], returns: t.u32 },
    g_dbus_connection_unregister_object: { args: [t.ptr, t.u32], returns: t.i32 },
    g_dbus_method_invocation_return_value: { args: [t.ptr, t.ptr], returns: t.void },
    g_dbus_method_invocation_return_dbus_error: { args: [t.ptr, t.ptr, t.ptr], returns: t.void },
    g_dbus_connection_emit_signal: { args: [t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.ptr], returns: t.i32 },
    g_dbus_proxy_new_sync: { args: [t.ptr, t.u32, t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.ptr], returns: t.ptr },
    g_dbus_proxy_get_name_owner: { args: [t.ptr], returns: t.ptr },
    g_dbus_proxy_get_cached_property: { args: [t.ptr, t.ptr], returns: t.ptr },
    g_dbus_proxy_call_sync: { args: [t.ptr, t.ptr, t.ptr, t.u32, t.i32, t.ptr, t.ptr], returns: t.ptr },
  });
  const glib = dlopen('libglib-2.0.so.0', {
    g_variant_parse: { args: [t.ptr, t.ptr, t.ptr, t.ptr, t.ptr], returns: t.ptr },
    g_variant_unref: { args: [t.ptr], returns: t.void },
    g_variant_get_boolean: { args: [t.ptr], returns: t.i32 },
    g_variant_get_child_value: { args: [t.ptr, t.u64], returns: t.ptr },
    g_variant_get_int32: { args: [t.ptr], returns: t.i32 },
    g_variant_get_string: { args: [t.ptr, t.ptr], returns: t.ptr },
    g_variant_n_children: { args: [t.ptr], returns: t.u64 },
    g_free: { args: [t.ptr], returns: t.void },
  });
  const object = dlopen('libgobject-2.0.so.0', {
    g_object_unref: { args: [t.ptr], returns: t.void },
    g_signal_connect_data: { args: [t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.u32], returns: t.u64 },
    g_signal_handler_disconnect: { args: [t.ptr, t.u64], returns: t.void },
  });
  const g = gio.symbols, v = glib.symbols, o = object.symbols;
  const z = (s: string) => ptr(Buffer.from(s + '\0'));
  const text = (p: Pointer | bigint | null) => p ? new CString(Number(p) as Pointer).toString() : '';
  const variant = (s: string) => { const value = v.g_variant_parse(null, z(s), null, null, null); if (!value) throw new Error('Invalid tray protocol value'); return value; };
  let preferences = { ...initial }, owner = '', disposed = false, revision = 1;
  let bus: Pointer | null = null, proxy: Pointer | null = null;
  const nodes: Pointer[] = [], registrations: number[] = [], signals: [Pointer, bigint][] = [], callbacks: JSCallback[] = [], tables: BigUint64Array[] = [];
  function properties(id: number) { return id === 0 ? "{'children-display': <'submenu'>}" : `{'label': <'${id === 1 ? 'Open Launchpad' : 'Quit'}'>, 'enabled': <${id !== 2 || !preferences.busy}>, 'visible': <true>}`; }
  function layout(id: number) { return `(${id}, ${properties(id)}, ${id === 0 ? `[<(1, ${properties(1)}, @av [])>, <(2, ${properties(2)}, @av [])>]` : '@av []'})`; }
  function emit(path: string, iface: string, name: string, value: string) { if (bus && !disposed) { const params = variant(value); g.g_dbus_connection_emit_signal(bus, null, z(path), z(iface), z(name), params, null); v.g_variant_unref(params); } }
  function child(p: Pointer, index: number, fn: (p: Pointer) => void) { const value = v.g_variant_get_child_value(p, index); if (value) { try { fn(Number(value) as Pointer); } finally { v.g_variant_unref(value); } } }
  function event(p: Pointer) { let id = 0, name = ''; child(p, 0, value => id = v.g_variant_get_int32(value)); child(p, 1, value => name = text(v.g_variant_get_string(value, null))); if (name === 'clicked') { if (id === 1) actions.show(); else if (id === 2) actions.quit(); } }
  function method(menu: boolean, name: string, params: Pointer) {
    if (!menu) { if (['Activate', 'SecondaryActivate', 'ContextMenu'].includes(name)) actions.show(); return '()'; }
    if (name === 'GetLayout') { let id = 0; child(params, 0, value => id = v.g_variant_get_int32(value)); return `(uint32 ${revision}, ${layout(id)})`; }
    if (name === 'GetGroupProperties') {
      const ids: number[] = []; child(params, 0, array => { for (let i = 0; i < Number(v.g_variant_n_children(array)); i++) child(array, i, value => ids.push(v.g_variant_get_int32(value))); });
      return `(@a(ia{sv}) [${ids.filter(id => [0, 1, 2].includes(id)).map(id => `(${id}, ${properties(id)})`).join(',')}],)`;
    }
    if (name === 'AboutToShow') return '(false,)';
    if (name === 'AboutToShowGroup') return '(@ai [], @ai [])';
    if (name === 'GetProperty') {
      let id = 0, prop = ''; child(params, 0, value => id = v.g_variant_get_int32(value)); child(params, 1, value => prop = text(v.g_variant_get_string(value, null)));
      return prop === 'label' ? `(< '${id === 1 ? 'Open Launchpad' : 'Quit'}'>,)` : prop === 'children-display' ? "(<'submenu'>,)" : `(<${prop === 'visible' || id !== 2 || !preferences.busy}>,)`;
    }
    if (name === 'Event') { event(params); return '()'; }
    if (name === 'EventGroup') { child(params, 0, array => { for (let i = 0; i < Number(v.g_variant_n_children(array)); i++) child(array, i, event); }); return '(@ai [],)'; }
    throw new Error('Unsupported tray method');
  }
  function property(menu: boolean, name: string) {
    if (menu) return ({ Version: 'uint32 3', TextDirection: "'ltr'", Status: "'normal'", IconThemePath: '@as []' } as Record<string, string>)[name] || "''";
    const pixels = () => `[(32, 32, [${trayPixels(alpha, preferences.accent).map((byte, i) => (i === 0 ? 'byte ' : '') + byte).join(',')}])]`;
    return ({ Category: "'ApplicationStatus'", Id: "'monkeforge-launchpad'", Title: "'MonkeForge Launchpad'", Status: preferences.exitToTray ? "'Active'" : "'Passive'", WindowId: 'uint32 0', IconName: "''", IconPixmap: name === 'IconPixmap' ? pixels() : '@a(iiay) []', OverlayIconName: "''", OverlayIconPixmap: '@a(iiay) []', AttentionIconName: "''", AttentionIconPixmap: '@a(iiay) []', AttentionMovieName: "''", ToolTip: "('', @a(iiay) [], 'MonkeForge Launchpad', '')", ItemIsMenu: 'false', Menu: `objectpath '${MENU}'` } as Record<string, string>)[name] || "''";
  }
  function ensure() {
    if (!proxy || disposed || !bus || g.g_dbus_connection_is_closed(bus)) { if (!disposed) actions.unavailable(); return false; }
    const name = g.g_dbus_proxy_get_name_owner(proxy), current = text(name); if (name) v.g_free(name);
    const host = g.g_dbus_proxy_get_cached_property(proxy, z('IsStatusNotifierHostRegistered'));
    const available = !!current && !!host && !!v.g_variant_get_boolean(host!); if (host) v.g_variant_unref(host);
    if (!available) { owner = ''; actions.unavailable(); return false; }
    if (owner !== current) {
      const params = variant(`('${ITEM}',)`);
      const result = g.g_dbus_proxy_call_sync(proxy, z('RegisterStatusNotifierItem'), params, 0, 1500, null, null); v.g_variant_unref(params);
      if (!result) { owner = ''; actions.unavailable(); return false; }
      v.g_variant_unref(result); owner = current;
    }
    return true;
  }
  function dispose() {
    if (disposed) return; disposed = true;
    for (const [target, id] of signals) o.g_signal_handler_disconnect(target, id);
    if (proxy) o.g_object_unref(proxy);
    if (bus) { for (const id of registrations) g.g_dbus_connection_unregister_object(bus, id); o.g_object_unref(bus); }
    for (const node of nodes) g.g_dbus_node_info_unref(node);
    for (const cb of callbacks) cb.close();
    tables.length = 0; // Keep native vtables pinned until their objects have been unregistered.
    object.close(); glib.close(); gio.close();
  }
  try {
    bus = Number(g.g_bus_get_sync(2, null, null)) as Pointer; if (!bus) throw new Error('Linux session bus is unavailable');
    g.g_dbus_connection_set_exit_on_close(bus, 0);
    for (const [path, iface, xml, menu] of [[ITEM, 'org.kde.StatusNotifierItem', itemXml, false], [MENU, 'com.canonical.dbusmenu', menuXml, true]] as const) {
      const node = g.g_dbus_node_info_new_for_xml(z(xml), null); if (!node) throw new Error('Could not create tray interface'); nodes.push(Number(node) as Pointer);
      const call = new JSCallback((_c: Pointer, _s: Pointer, _p: Pointer, _i: Pointer, name: Pointer, params: Pointer, invocation: Pointer) => {
        try { const value = variant(method(menu, text(name), params)); g.g_dbus_method_invocation_return_value(invocation, value); v.g_variant_unref(value); }
        catch (error) { g.g_dbus_method_invocation_return_dbus_error(invocation, z('org.freedesktop.DBus.Error.Failed'), z(String(error))); }
      }, { args: [t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.ptr], returns: t.void });
      const get = new JSCallback((_c: Pointer, _s: Pointer, _p: Pointer, _i: Pointer, name: Pointer) => { try { return variant(property(menu, text(name))); } catch { return null; } }, { args: [t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.ptr], returns: t.ptr });
      callbacks.push(call, get); const table = new BigUint64Array(11); table[0] = BigInt(call.ptr!); table[1] = BigInt(get.ptr!); tables.push(table);
      const id = g.g_dbus_connection_register_object(bus, z(path), g.g_dbus_node_info_lookup_interface(node, z(iface)), ptr(table), null, null, null);
      if (!id) throw new Error('Could not register tray interface'); registrations.push(id);
    }
    proxy = Number(g.g_dbus_proxy_new_sync(bus, 4, null, z('org.kde.StatusNotifierWatcher'), z('/StatusNotifierWatcher'), z('org.kde.StatusNotifierWatcher'), null, null)) as Pointer;
    if (!proxy) throw new Error('Could not connect to the Linux tray');
    const changed = new JSCallback(() => { if (!disposed) { try { ensure(); } catch { actions.unavailable(); } } }, { args: [t.ptr, t.ptr, t.ptr, t.ptr], returns: t.void }); callbacks.push(changed);
    // Both watcher restarts and host removal must restore a hidden window.
    for (const signal of ['notify::g-name-owner', 'g-properties-changed']) signals.push([proxy, BigInt(o.g_signal_connect_data(proxy, z(signal), changed.ptr, null, null, 0))]);
    signals.push([bus, BigInt(o.g_signal_connect_data(bus, z('closed'), changed.ptr, null, null, 0))]);
    ensure();
    return { ensure, update(next: NativePreferences) { const old = preferences; preferences = { ...next }; if (old.accent !== next.accent) emit(ITEM, 'org.kde.StatusNotifierItem', 'NewIcon', '()'); if (old.exitToTray !== next.exitToTray) emit(ITEM, 'org.kde.StatusNotifierItem', 'NewStatus', `('${next.exitToTray ? 'Active' : 'Passive'}',)`); if (old.busy !== next.busy) emit(MENU, 'com.canonical.dbusmenu', 'LayoutUpdated', `(uint32 ${++revision}, 0)`); ensure(); }, dispose };
  } catch (error) { dispose(); throw error; }
}
export function createLinuxTray(handle: Pointer, alpha: Uint8Array, initial: NativePreferences, report: (message: string) => void): TrayController {
  const gtk = dlopen('libgtk-4.so.1', { gtk_widget_set_visible: { args: [t.ptr, t.i32], returns: t.void }, gtk_window_present: { args: [t.ptr], returns: t.void }, gtk_window_close: { args: [t.ptr], returns: t.void } });
  const object = dlopen('libgobject-2.0.so.0', { g_signal_connect_data: { args: [t.ptr, t.ptr, t.ptr, t.ptr, t.ptr, t.u32], returns: t.u64 }, g_signal_handler_disconnect: { args: [t.ptr, t.u64], returns: t.void } });
  let destroyed = false;
  let notifier: ReturnType<typeof createStatusNotifier>;
  const behavior = createTrayBehavior(initial, {
    available: () => notifier.ensure(),
    show: () => { if (!destroyed) gtk.symbols.gtk_window_present(handle); },
    hide: () => { if (!destroyed) gtk.symbols.gtk_widget_set_visible(handle, 0); },
    close: () => { if (!destroyed) gtk.symbols.gtk_window_close(handle); }, report,
  });
  try { notifier = createStatusNotifier(alpha, initial, behavior); }
  catch (error) { object.close(); gtk.close(); throw error; }
  const close = new JSCallback(() => { try { return Number(behavior.closeRequested()); } catch (error) { behavior.show(); report(error instanceof Error ? error.message : 'Tray operation failed'); return 1; } }, { args: [t.ptr, t.ptr], returns: t.i32 });
  const signal = object.symbols.g_signal_connect_data(handle, ptr(Buffer.from('close-request\0')), close.ptr, null, null, 0);
  const destroy = new JSCallback(() => { destroyed = true; }, { args: [t.ptr, t.ptr], returns: t.void });
  const destroySignal = object.symbols.g_signal_connect_data(handle, ptr(Buffer.from('destroy\0')), destroy.ptr, null, null, 0);
  return { hide: () => { behavior.hide(); }, update(next) { behavior.update(next); notifier.update(next); }, dispose() { if (!destroyed) { object.symbols.g_signal_handler_disconnect(handle, signal); object.symbols.g_signal_handler_disconnect(handle, destroySignal); } close.close(); destroy.close(); notifier.dispose(); object.close(); gtk.close(); } };
}
