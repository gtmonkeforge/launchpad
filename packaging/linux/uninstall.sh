#!/usr/bin/env bash
set -euo pipefail
data_dir=${XDG_DATA_HOME:-"$HOME/.local/share"}
[[ $data_dir = /* && $data_dir != *$'\n'* && $data_dir != *$'\r'* ]] || { echo 'XDG_DATA_HOME must be an absolute path without line breaks.' >&2; exit 1; }
app_dir="$data_dir/monkeforge-launchpad"
rm -f -- "$data_dir/applications/monkeforge-launchpad.desktop" "$data_dir/icons/hicolor/scalable/apps/monkeforge-launchpad.svg"
if [[ -f $app_dir/Launchpad ]]; then rm -rf -- "$app_dir"; fi
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database "$data_dir/applications" || true; fi
if command -v gtk-update-icon-cache >/dev/null 2>&1 && [[ -f $data_dir/icons/hicolor/index.theme ]]; then gtk-update-icon-cache -f -t "$data_dir/icons/hicolor" || true; fi
printf 'Uninstalled MonkeForge Launchpad. Your settings, sign-in and installed mods were kept.\n'
