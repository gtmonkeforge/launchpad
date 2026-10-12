import { dlopen, FFIType as t, ptr, type Pointer } from 'bun:ffi';
export type WindowAction = 'status' | 'minimize' | 'maximize' | 'close' | 'drag' | 'resize';
export interface WindowControls { perform(action: WindowAction, edge?: number): { maximized: boolean }; dispose(): void }
/** Called on the webview thread; never pass window handles to the HTTP worker. */
export function createWindowControls(handle: Pointer): WindowControls {
  if (process.platform === 'win32') {
    const lib = dlopen('user32.dll', {
      GetWindowLongPtrW: { args: [t.ptr, t.i32], returns: t.i64_fast },
      SetWindowLongPtrW: { args: [t.ptr, t.i32, t.i64_fast], returns: t.i64_fast },
      SetWindowPos: { args: [t.ptr, t.ptr, t.i32, t.i32, t.i32, t.i32, t.u32], returns: t.i32 },
      ShowWindow: { args: [t.ptr, t.i32], returns: t.i32 },
      IsZoomed: { args: [t.ptr], returns: t.i32 },
      ReleaseCapture: { args: [], returns: t.i32 },
      GetCursorPos: { args: [t.ptr], returns: t.i32 },
      PostMessageW: { args: [t.ptr, t.u32, t.u64_fast, t.i64_fast], returns: t.i32 },
    });
    const s = lib.symbols;
    const style = Number(s.GetWindowLongPtrW(handle, -16));
    // Remove the system caption, preserving resize borders, taskbar and Alt+F4.
    s.SetWindowLongPtrW(handle, -16, style & ~0x00c00000);
    s.SetWindowPos(handle, null, 0, 0, 0, 0, 0x0027); // FRAMECHANGED | NOMOVE | NOSIZE | NOZORDER
    return {
      perform(action) {
        if (action === 'minimize') s.ShowWindow(handle, 6);
        else if (action === 'maximize') s.ShowWindow(handle, s.IsZoomed(handle) ? 9 : 3);
        else if (action === 'close') s.PostMessageW(handle, 0x0010, 0, 0);
        else if (action === 'drag') {
          const point = new Int32Array(2); s.GetCursorPos(ptr(point));
          const position = ((point[1]! & 0xffff) << 16) | (point[0]! & 0xffff);
          s.ReleaseCapture(); s.PostMessageW(handle, 0x00a1, 2, position);
        }
        else if (action !== 'status') throw new Error('Unsupported window action');
        return { maximized: !!s.IsZoomed(handle) };
      },
      dispose: () => lib.close(),
    };
  }
  if (process.platform === 'linux') {
    const lib = dlopen('libgtk-4.so.1', {
      gtk_window_set_decorated: { args: [t.ptr, t.i32], returns: t.void },
      gtk_window_minimize: { args: [t.ptr], returns: t.void },
      gtk_window_maximize: { args: [t.ptr], returns: t.void },
      gtk_window_unmaximize: { args: [t.ptr], returns: t.void },
      gtk_window_is_maximized: { args: [t.ptr], returns: t.i32 },
      gtk_window_close: { args: [t.ptr], returns: t.void },
      gtk_native_get_surface: { args: [t.ptr], returns: t.ptr },
      gdk_surface_get_display: { args: [t.ptr], returns: t.ptr },
      gdk_display_get_default_seat: { args: [t.ptr], returns: t.ptr },
      gdk_seat_get_pointer: { args: [t.ptr], returns: t.ptr },
      gdk_device_get_timestamp: { args: [t.ptr], returns: t.u32 },
      gdk_surface_get_device_position: { args: [t.ptr, t.ptr, t.ptr, t.ptr, t.ptr], returns: t.i32 },
      gdk_toplevel_begin_move: { args: [t.ptr, t.ptr, t.i32, t.f64, t.f64, t.u32], returns: t.void },
      gdk_toplevel_begin_resize: { args: [t.ptr, t.i32, t.ptr, t.i32, t.f64, t.f64, t.u32], returns: t.void },
    });
    const s = lib.symbols;
    s.gtk_window_set_decorated(handle, 0);
    return {
      perform(action, edge) {
        if (action === 'minimize') s.gtk_window_minimize(handle);
        else if (action === 'maximize') { if (s.gtk_window_is_maximized(handle)) s.gtk_window_unmaximize(handle); else s.gtk_window_maximize(handle); }
        else if (action === 'close') { s.gtk_window_close(handle); return { maximized: false }; }
        else if (action === 'drag' || action === 'resize') {
          const surface = s.gtk_native_get_surface(handle);
          if (!surface) throw new Error('Window surface is unavailable');
          const display = s.gdk_surface_get_display(surface), seat = s.gdk_display_get_default_seat(display);
          if (!seat) throw new Error('Pointer seat is unavailable');
          const device = s.gdk_seat_get_pointer(seat);
          if (!device) throw new Error('Pointer device is unavailable');
          const x = new Float64Array(1), y = new Float64Array(1);
          if (s.gdk_surface_get_device_position(surface, device, ptr(x), ptr(y), null)) {
            const time = s.gdk_device_get_timestamp(device);
            if (action === 'resize') {
              if (!Number.isInteger(edge) || edge! < 0 || edge! > 7) throw new Error('Invalid resize edge');
              s.gdk_toplevel_begin_resize(surface, edge!, device, 1, x[0]!, y[0]!, time);
            } else s.gdk_toplevel_begin_move(surface, device, 1, x[0]!, y[0]!, time);
          }
        } else if (action !== 'status') throw new Error('Unsupported window action');
        return { maximized: !!s.gtk_window_is_maximized(handle) };
      },
      dispose: () => lib.close(),
    };
  }
  throw new Error('Launchpad supports Windows and Linux');
}
