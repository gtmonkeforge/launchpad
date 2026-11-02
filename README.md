# MonkeForge Launchpad
You can get releases & information about MonkeForge Launchpad at https://monkeforge.org/app.


## Release packages

Run `bun install --frozen-lockfile`, `bun run build`, then `bun run package`.
Packaging requires NSIS 3 (`makensis` on PATH, or set `MAKENSIS`) and `tar`.
Use `bun run package:win64` or `bun run package:tux64` to package one platform.
Run `bun run package:verify` on Linux to check package checksums and exercise
install, upgrade, desktop-entry validation, standalone launch, and uninstall
in isolated temporary directories. Outputs and SHA-256 checksums are written to `dist/`:

- `launchpad-<version>-windows-x64-setup.exe`: installs for the current user and adds
  a Start menu shortcut. Includes an uninstaller in Windows Installed Apps; does
  not launch the program, add a desktop shortcut, or enable startup at install time.
- `launchpad-<version>-linux-x64.tar.gz`: executable, branded icon, `install.sh`,
  `uninstall.sh`, license, and instructions. Run `./install.sh` without sudo to add
  the application to the current user's applications menu. Uninstall removes the
  program, menu entry, icon, and managed startup entry, preserving preferences/mods.

Close Launchpad before upgrading or uninstalling. Packages are unsigned.

## Social and MonkeShield

Social contains Friends, Chat, and Groups subtabs; existing section links remain supported.
MonkeShield scans every DLL recursively in the selected loader's plugin folders,
including files installed outside Launchpad. A scan starts automatically when this session has no scan and a plugins folder is
available. The Scan button starts a new scan, and successful mod installs/updates trigger one automatically (once at the end of a
batch update). Each DLL is uploaded as raw bytes to `https://shield.monkeforge.org/api/scan`.
Scan progress and per-file checks, diagnostics, risk scores, and JSON reports are
available in MonkeShield for the current session. Scans do not execute or change DLLs.
Symbolic links are skipped and reported; errors and incomplete analyses remain
marked for review. Results are kept for the current session and cleared when
Launchpad restarts.


## Linux tray

Enable **Settings → Close to tray** to keep Launchpad running when its window is
closed. The tray menu provides **Open Launchpad** and **Quit**. Startup launches
can start hidden when this preference is enabled. A desktop with StatusNotifier
tray support is required (GNOME commonly uses an AppIndicator extension). If no
tray host is available, closing exits normally; if a host disappears while the
window is hidden, Launchpad restores the window. Quit is disabled during mod
operations and scans. No GTK 3 or AppIndicator library is required by Launchpad.

`bun run tray:verify` tests the protocol on a private D-Bus session, using
`dbus-daemon`, `gdbus`, and Python with PyGObject. Set `VERIFY_GTK_TRAY=1` to also
exercise GTK window closing with the headless `gtk4-broadwayd` backend. Set
`DBUS_DAEMON` if the test daemon is not on PATH. These are test dependencies only.


Proton setup can run while Steam is open. Existing native-first `winhttp`
WINEDLLOVERRIDES entries are left unchanged, including custom quoting and other
launch arguments. Missing overrides are added with a backup of the original
Steam configuration.


## Mod loaders

Choose **BepInEx 5** or **MelonLoader** under **Settings → Mod loader**. BepInEx 5
is the default. The choice applies when installing mods or next launching Gorilla
Tag. Launchpad installs the selected loader's official Windows x64 package, selects
matching catalog downloads, and uses `winhttp` (BepInEx) or `version` (MelonLoader)
for Proton overrides. Existing valid overrides and unrelated launch arguments
are retained.

Loader switching moves the inactive bootstrap into `.launchpad/disabled-loaders`
and restores the selected bootstrap. Existing runtime and mod files are kept;
mods are not converted between loaders. Browse and Installed show the selected
loader's mods, and incompatible releases cannot be installed. MonkeShield scans
`BepInEx/plugins` for BepInEx, or both `Mods` and `Plugins` for MelonLoader.
