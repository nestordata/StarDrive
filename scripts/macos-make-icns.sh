#!/usr/bin/env bash
# Convert Icons/Mars.ico → AppIcon.icns for the macOS .app / DMG volume icon.
#
# Usage: bash scripts/macos-make-icns.sh [out.icns]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ICO="${ROOT}/Icons/Mars.ico"
OUT="${1:-${ROOT}/Icons/AppIcon.icns}"

[[ -f "$ICO" ]] || { echo "ERROR: missing $ICO" >&2; exit 1; }
command -v sips >/dev/null || { echo "ERROR: sips required" >&2; exit 1; }
command -v iconutil >/dev/null || { echo "ERROR: iconutil required" >&2; exit 1; }

TMP="$(mktemp -d /tmp/mars-icns.XXXXXX)"
cleanup() { rm -rf "$TMP"; }
trap cleanup EXIT

# sips reads multi-size .ico and emits a usable PNG (typically the largest).
PNG="$TMP/mars.png"
sips -s format png "$ICO" --out "$PNG" >/dev/null

ICONSET="$TMP/AppIcon.iconset"
mkdir -p "$ICONSET"
make_size() {
  local px="$1" name="$2"
  sips -z "$px" "$px" "$PNG" --out "$ICONSET/$name" >/dev/null
}
make_size 16   icon_16x16.png
make_size 32   icon_16x16@2x.png
make_size 32   icon_32x32.png
make_size 64   icon_32x32@2x.png
make_size 128  icon_128x128.png
make_size 256  icon_128x128@2x.png
make_size 256  icon_256x256.png
make_size 512  icon_256x256@2x.png
make_size 512  icon_512x512.png
make_size 1024 icon_512x512@2x.png

mkdir -p "$(dirname "$OUT")"
iconutil -c icns "$ICONSET" -o "$OUT"
ls -lh "$OUT"
echo "Wrote $OUT"
