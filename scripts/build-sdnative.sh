#!/usr/bin/env bash
# Build libSDNative for the current host (osx-arm64 / linux-*).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD_DIR="${ROOT}/SDNative/build-${OSTYPE:-host}"
OUT_DIR="${1:-${ROOT}/game}"
ENABLE_FBX="${SDNATIVE_ENABLE_FBX:-ON}"

if [[ ! -f "${ROOT}/SDNative/ReCpp/src/rpp/strview.h" ]] || [[ ! -f "${ROOT}/SDNative/NanoMesh/src/Mesh.cpp" ]]; then
  echo "ERROR: SDNative submodules not checked out."
  echo "Run:  git submodule update --init --recursive"
  echo "Required: SDNative/ReCpp and SDNative/NanoMesh"
  exit 1
fi

# Assimp OBJ UV pools: map 0 < numCoords < numVerts as SharedElements.
# Kept as a StarDrive patch until it lands on gkapulis/NanoMesh (no push access here).
# Still useful when FBX is unavailable (Linux) or as OBJ fallback.
NANOMESH_PATCH="${ROOT}/SDNative/patches/nanomesh-assimp-uv-sharedelements.patch"
if [[ -f "${NANOMESH_PATCH}" ]]; then
  if git -C "${ROOT}/SDNative/NanoMesh" apply --reverse --check "${NANOMESH_PATCH}" >/dev/null 2>&1; then
    : # already applied
  else
    git -C "${ROOT}/SDNative/NanoMesh" apply "${NANOMESH_PATCH}" \
      || echo "WARN: NanoMesh Assimp UV patch failed to apply (already applied or conflict)"
  fi
fi

if [[ "$(uname -s)" == "Darwin" && "${ENABLE_FBX}" == "ON" ]]; then
  FBX_DYLIB="${ROOT}/SDNative/3rdparty/fbxsdk/macos/libfbxsdk.dylib"
  if [[ ! -f "${FBX_DYLIB}" ]]; then
    echo "==> Fetching Autodesk FBX SDK macOS runtime…"
    bash "${ROOT}/scripts/fetch-fbxsdk-macos.sh"
  fi
fi

mkdir -p "${BUILD_DIR}" "${OUT_DIR}"
cmake -S "${ROOT}/SDNative" -B "${BUILD_DIR}" \
  -DCMAKE_BUILD_TYPE=Release \
  -DSDNATIVE_ENABLE_FBX="${ENABLE_FBX}" \
  -DCMAKE_CXX_STANDARD=20
JOBS="$(sysctl -n hw.ncpu 2>/dev/null || nproc 2>/dev/null || echo 4)"
cmake --build "${BUILD_DIR}" -j"${JOBS}"

if [[ "$(uname)" == "Darwin" ]]; then
  cp -f "${BUILD_DIR}/libSDNative.dylib" "${OUT_DIR}/libSDNative.dylib"
  if [[ -f "${ROOT}/SDNative/3rdparty/fbxsdk/macos/libfbxsdk.dylib" ]]; then
    cp -f "${ROOT}/SDNative/3rdparty/fbxsdk/macos/libfbxsdk.dylib" "${OUT_DIR}/libfbxsdk.dylib"
    codesign --force --sign - "${OUT_DIR}/libfbxsdk.dylib" 2>/dev/null || true
    echo "Installed ${OUT_DIR}/libfbxsdk.dylib"
  fi
  echo "Installed ${OUT_DIR}/libSDNative.dylib"
else
  cp -f "${BUILD_DIR}/libSDNative.so" "${OUT_DIR}/libSDNative.so"
  echo "Installed ${OUT_DIR}/libSDNative.so"
fi
