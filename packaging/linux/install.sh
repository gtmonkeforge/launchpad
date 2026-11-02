#!/usr/bin/env bash
set -euo pipefail
if [[ $(uname -m) != x86_64 ]]; then
    echo 'MonkeForge Launchpad requires x86-64 Linux.' >&2
    exit 1
fi
source_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
data_dir=${XDG_DATA_HOME:-"$HOME/.local/share"}
[[ $data_dir = /* && $data_dir != *$'\n'* && $data_dir != *$'\r'* ]] || { echo 'XDG_DATA_HOME must be an absolute path without line breaks.' >&2; exit 1; }
app_dir="$data_dir/monkeforge-launchpad"
applications_dir="$data_dir/applications"
icon_dir="$data_dir/icons/hicolor/scalable/apps"
mkdir -p -- "$app_dir" "$applications_dir" "$icon_dir"
install -m 755 -- "$source_dir/launchpad-tux64" "$app_dir/launchpad-tux64.new"
mv -f -- "$app_dir/launchpad-tux64.new" "$app_dir/launchpad-tux64"
install -m 755 -- "$source_dir/uninstall.sh" "$app_dir/uninstall.sh"
install -m 644 -- "$source_dir/LICENSE.md" "$app_dir/LICENSE.md"
install -m 644 -- "$source_dir/monkeforge-launchpad.svg" "$icon_dir/monkeforge-launchpad.svg"
exec_path=$(printf '%s' "$app_dir/launchpad-tux64" | sed -e 's/%/%%/g' -e 's/["`$\\]/\\&/g' -e 's/\\/\\\\/g')
cat > "$applications_dir/monkeforge-launchpad.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Version=1.0
Name=MonkeForge Launchpad
Comment=Launch Gorilla Tag and manage your MonkeForge mods
Exec=/usr/bin/env -- "$exec_path"
Icon=$icon_dir/monkeforge-launchpad.svg
Terminal=false
Categories=Game;Utility;
StartupNotify=true
DESKTOP
chmod 644 -- "$applications_dir/monkeforge-launchpad.desktop"
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database "$applications_dir" || true; fi
if command -v gtk-update-icon-cache >/dev/null 2>&1 && [[ -f $data_dir/icons/hicolor/index.theme ]]; then gtk-update-icon-cache -f -t "$data_dir/icons/hicolor" || true; fi
printf 'Installed MonkeForge Launchpad. Open it from your applications menu.\nUninstall: bash %q\n' "$app_dir/uninstall.sh"
