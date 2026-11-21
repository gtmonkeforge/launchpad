#!/usr/bin/env bash
# Installs the Launchpad server as a systemd service. Run it as root from a published build:
#
#   dotnet publish src/Launchpad.Server -c Release -r linux-x64 --self-contained -o out
#   sudo out/install.sh              installs to /opt/launchpad-server and starts the service
#
# Upgrading is the same command: the server files are replaced, and .env and data/ are kept.
set -euo pipefail

TARGET="${1:-/opt/launchpad-server}"
SOURCE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVICE=/etc/systemd/system/launchpad-server.service

[[ $EUID -eq 0 ]] || { echo "Run this as root (sudo ./install.sh)." >&2; exit 1; }
[[ -x "$SOURCE/LaunchpadServer" ]] || { echo "Run this from a published Launchpad server build (LaunchpadServer not found next to it)." >&2; exit 1; }
command -v systemctl >/dev/null || { echo "systemd is required." >&2; exit 1; }

id launchpad &>/dev/null || useradd --system --home-dir "$TARGET" --shell /usr/sbin/nologin launchpad
mkdir -p "$TARGET/data"

if systemctl is-active --quiet launchpad-server; then
    echo "==> Stopping the running server"
    systemctl stop launchpad-server
fi

echo "==> Copying the server to $TARGET"
for item in "$SOURCE"/*; do
    name="$(basename "$item")"
    case "$name" in
        .env|data|install.sh) continue ;;
    esac
    rm -rf "${TARGET:?}/$name"
    cp -a "$item" "$TARGET/"
done

if [[ ! -f "$TARGET/.env" ]]; then
    cp "$SOURCE/env.example" "$TARGET/.env"
    echo "==> Created $TARGET/.env. Fill in LAUNCHPAD_CLIENT_SECRET, then: systemctl restart launchpad-server"
fi

chmod 600 "$TARGET/.env"
chown -R launchpad:launchpad "$TARGET"

echo "==> Installing $SERVICE"
sed "s#/opt/launchpad-server#$TARGET#g" "$SOURCE/launchpad-server.service" > "$SERVICE"
systemctl daemon-reload
systemctl enable --now launchpad-server

echo
echo "The Launchpad server is running on http://127.0.0.1:5080. Put it behind your reverse proxy as launchpad.monkeforge.org."
echo "Logs: journalctl -u launchpad-server -f"
