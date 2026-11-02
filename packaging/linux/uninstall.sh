#!/usr/bin/env bash
set -euo pipefail
data_dir=${XDG_DATA_HOME:-"$HOME/.local/share"}
config_dir=${XDG_CONFIG_HOME:-"$HOME/.config"}
[[ $data_dir = /* && $data_dir != *$'\n'* && $data_dir != *$'\r'* ]] || { echo 'XDG_DATA_HOME must be an absolute path without line breaks.' >&2; exit 1; }
app_dir="$data_dir/monkeforge-launchpad"
rm -f -- "$data_dir/applications/monkeforge-launchpad.desktop" "$data_dir/icons/hicolor/scalable/apps/monkeforge-launchpad.svg"
startup="$config_dir/autostart/monkeforge-launchpad.desktop"
if [[ -f $startup ]] && grep -qF 'MonkeForge Launchpad managed startup' "$startup"; then rm -f -- "$startup"; fi
rm -f -- "$app_dir/launchpad-tux64" "$app_dir/LICENSE.md" "$app_dir/uninstall.sh"
if [[ -d $app_dir ]]; then rmdir -- "$app_dir" 2>/dev/null || true; fi
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database "$data_dir/applications" || true; fi
if command -v gtk-update-icon-cache >/dev/null 2>&1 && [[ -f $data_dir/icons/hicolor/index.theme ]]; then gtk-update-icon-cache -f -t "$data_dir/icons/hicolor" || true; fi
printf 'Uninstalled MonkeForge Launchpad. Preferences and installed mods were preserved.\n'
