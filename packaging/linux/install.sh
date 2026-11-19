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
[[ -f $source_dir/Launchpad ]] || { echo "Launchpad wasn't found next to install.sh. Run it from the extracted archive." >&2; exit 1; }
if [[ $source_dir != "$app_dir" ]]; then
    staging="$app_dir.new"
    rm -rf -- "$staging"
    mkdir -p -- "$staging"
    cp -a -- "$source_dir/." "$staging/"
    rm -f -- "$staging/install.sh"
    if [[ -f $app_dir/Launchpad ]]; then rm -rf -- "$app_dir"; fi
    mv -- "$staging" "$app_dir"
fi
chmod 755 -- "$app_dir/Launchpad" "$app_dir/uninstall.sh"
mkdir -p -- "$applications_dir" "$icon_dir"
install -m 644 -- "$app_dir/monkeforge-launchpad.svg" "$icon_dir/monkeforge-launchpad.svg"
exec_path=$(printf '%s' "$app_dir/Launchpad" | sed -e 's/%/%%/g' -e 's/["`$\\]/\\&/g' -e 's/\\/\\\\/g')
cat > "$applications_dir/monkeforge-launchpad.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Version=1.0
Name=MonkeForge Launchpad
Comment=Browse MonkeForge and manage your mods
Exec="$exec_path"
Icon=monkeforge-launchpad
Terminal=false
Categories=Game;Utility;
StartupNotify=true
DESKTOP
chmod 644 -- "$applications_dir/monkeforge-launchpad.desktop"
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database "$applications_dir" || true; fi
if command -v gtk-update-icon-cache >/dev/null 2>&1 && [[ -f $data_dir/icons/hicolor/index.theme ]]; then gtk-update-icon-cache -f -t "$data_dir/icons/hicolor" || true; fi
printf 'Installed MonkeForge Launchpad. Open it from your applications menu.\nUninstall: bash %q\n' "$app_dir/uninstall.sh"
