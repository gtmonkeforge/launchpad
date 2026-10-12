# Deployment builds

Install dependencies with Bun 1.4.3 or newer, then run:

```sh
bun install --frozen-lockfile
bun run typecheck
bun test
bun run build
bun run build:verify
```

The build creates both standalone artifacts in `dist/`:

- `launchpad-win64.exe` — Windows x64 GUI executable, with the Launchpad icon and no console window.
- `launchpad-tux64` — Linux x64 executable for glibc systems.

Use `bun run build:win64` or `bun run build:tux64` for a single target. Each artifact has a matching `.sha256` checksum file. Builds embed the prebuilt frontend, HTML, styles, SVG logo, tray mask, HTTP worker, Bun runtime, and the target's native webview library. Deployed machines need no Bun installation, source files, or `node_modules`.

On Windows, `./build.ps1` builds both targets; `./build.ps1 win64` or `./build.ps1 tux64` builds one. This wrapper also finds Bun in its default user installation when it is absent from PATH. The Windows build uses `logo.ico` if present, otherwise `ui/launchpad.ico`.

## Runtime requirements

Windows requires the Microsoft WebView2 runtime. Linux requires GTK 4, WebKitGTK 6, and `xdg-open`. On Debian/Ubuntu, the native packages are `libgtk-4-1` and `libwebkitgtk-6.0-4`. After copying the Linux artifact:

```sh
chmod +x launchpad-tux64
./launchpad-tux64
```

The embedded native library is extracted to a verified, content-addressed per-user cache because OS dynamic loaders need a real filesystem path. Startup entries for compiled builds launch the executable directly. Re-save the startup setting after moving the executable. The app keeps settings and game manifests outside the binary.

## Verification

`bun run build:verify` checks both artifact architectures and runs the host platform's `--self-check` from an isolated temporary directory with no source tree or dependencies. The self-check loads the native library, starts the embedded worker, and verifies the bundled UI routes without opening a window or registering startup. Cross-compilation does not validate the other platform's runtime; run verification on Linux with the required packages before releasing its build.

Run a deployed executable with `--browser` to preview its bundled UI instead of opening a native window. Neither `--browser` nor `--self-check` applies first-run startup registration.

The build adapter makes `webview-bun` imports static and restricts native packaging to the target library, without editing dependency files. UI bundling happens before compilation. `scripts/build-tray-mask.py` regenerates the native mask and Windows ICO from the supplied SVG (Pillow required only for regeneration); both generated assets are committed inputs to builds.

These artifacts are unsigned. Production signing and Linux package distribution are separate release steps.
