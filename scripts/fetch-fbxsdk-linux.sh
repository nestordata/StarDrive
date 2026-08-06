#!/usr/bin/env bash
# Placeholder: vendor Autodesk FBX SDK runtime for Linux x64 (Phase 5a).
# Autodesk publishes Linux clang builds; download requires accepting their EULA
# (same 2020.3.7 line as Windows/macOS).
#
# Expected layout after fetch:
#   SDNative/3rdparty/fbxsdk/linux/libfbxsdk.so
# Headers already live in SDNative/3rdparty/fbxsdk/ (shared with other hosts).
#
# Usage: bash scripts/fetch-fbxsdk-linux.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="${ROOT}/SDNative/3rdparty/fbxsdk/linux"

echo "Linux FBX SDK fetch is not automated yet."
echo "Manual steps:"
echo "  1. Download FBX SDK 2020.3.7 Linux clang package from Autodesk APS."
echo "  2. Extract libfbxsdk.so (release) into:"
echo "       ${DEST}/libfbxsdk.so"
echo "  3. Rebuild: SDNATIVE_ENABLE_FBX=ON bash scripts/build-sdnative.sh"
echo ""
echo "CMake will enable ENABLE_FBX_MESH_LOADER when ${DEST}/libfbxsdk.so exists."
mkdir -p "${DEST}"
if [[ -f "${DEST}/libfbxsdk.so" ]]; then
  ls -lh "${DEST}/libfbxsdk.so"
  echo "OK: runtime present"
  exit 0
fi
exit 1
