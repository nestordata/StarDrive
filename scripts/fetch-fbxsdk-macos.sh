#!/usr/bin/env bash
# Download Autodesk FBX SDK 2020.3.7 for macOS and vendor the arm64 runtime dylib.
# Headers are already in SDNative/3rdparty/fbxsdk/ (same version as Windows).
#
# Usage: bash scripts/fetch-fbxsdk-macos.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="${ROOT}/SDNative/3rdparty/fbxsdk/macos"
URL='https://damassets.autodesk.net/content/dam/autodesk/www/files/fbx202037_fbxsdk_clang_mac.pkg.tgz'
UA='Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "ERROR: this script only runs on macOS" >&2
  exit 1
fi

TMP="$(mktemp -d /tmp/fbxsdk-mac.XXXXXX)"
cleanup() { rm -rf "$TMP"; }
trap cleanup EXIT

echo "==> Downloading FBX SDK 2020.3.7 (clang mac)…"
curl -L --fail -A "$UA" -e 'https://aps.autodesk.com/' -o "$TMP/sdk.pkg.tgz" "$URL"

echo "==> Extracting…"
cd "$TMP"
tar -xzf sdk.pkg.tgz
mkdir pkg && cd pkg
xar -xf ../*.pkg
mkdir root && cd root
gunzip -dc ../Root.pkg/Payload | cpio -idmu

SDK_LIB="$TMP/pkg/root/Applications/Autodesk/FBX SDK/2020.3.7/lib/clang/release/libfbxsdk.dylib"
[[ -f "$SDK_LIB" ]] || { echo "ERROR: libfbxsdk.dylib not found in package" >&2; exit 1; }

mkdir -p "$DEST"
lipo "$SDK_LIB" -thin arm64 -output "$DEST/libfbxsdk.dylib"
chmod u+w "$DEST/libfbxsdk.dylib"
install_name_tool -id "@rpath/libfbxsdk.dylib" "$DEST/libfbxsdk.dylib"
codesign --force --sign - "$DEST/libfbxsdk.dylib"
codesign -v "$DEST/libfbxsdk.dylib"
lipo -info "$DEST/libfbxsdk.dylib"
ls -lh "$DEST/libfbxsdk.dylib"
echo "==> Vendored ${DEST}/libfbxsdk.dylib"
