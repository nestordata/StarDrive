#!/usr/bin/env bash
# Install a user-local .desktop entry + optional icon for a DesktopVK Linux publish.
#
# Usage:
#   bash scripts/install-linux-desktop.sh /path/to/publish-dir
# Example after publish:
#   bash scripts/install-linux-desktop.sh artifacts/linux-x64
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH="${1:-}"
[[ -n "$PUBLISH" ]] || { echo "Usage: $0 /path/to/StarDrive-publish" >&2; exit 1; }
PUBLISH="$(cd "$PUBLISH" && pwd)"
BIN="${PUBLISH}/StarDrive"
[[ -x "$BIN" ]] || { echo "ERROR: executable not found: $BIN" >&2; exit 1; }

APP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
mkdir -p "$APP_DIR"
DESKTOP="${APP_DIR}/io.stardriveteam.blackbox.desktop"

cat > "$DESKTOP" <<EOF
[Desktop Entry]
Type=Application
Name=StarDrive BlackBox
Comment=StarDrive BlackBox (DesktopVK)
Exec=${BIN}
Path=${PUBLISH}
Terminal=false
Categories=Game;
StartupNotify=true
EOF
chmod 644 "$DESKTOP"

echo "Installed ${DESKTOP}"
echo "Launch from your app menu, or: ${BIN}"
echo ""
echo "AppImage packaging (optional): wrap ${PUBLISH} with appimagetool once a"
echo "linuxdeploy recipe is added; until then distribute the publish folder / tarball."
