#!/usr/bin/env bash
# Build libSDNative for macOS Apple Silicon (osx-arm64).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD_DIR="${ROOT}/SDNative/build-darwin"
OUT_DIR="${1:-${ROOT}/game}"
ENABLE_FBX="${SDNATIVE_ENABLE_FBX:-ON}"
ENABLE_FFMPEG="${SDNATIVE_ENABLE_FFMPEG:-ON}"

[[ "$(uname -s)" == "Darwin" ]] || {
  echo "ERROR: build-sdnative.sh is macOS-only (Apple Silicon DesktopVK)." >&2
  exit 1
}

if [[ ! -f "${ROOT}/SDNative/ReCpp/src/rpp/strview.h" ]] || [[ ! -f "${ROOT}/SDNative/NanoMesh/src/Mesh.cpp" ]]; then
  echo "ERROR: SDNative submodules not checked out."
  echo "Run:  git submodule update --init --recursive"
  echo "Required: SDNative/ReCpp and SDNative/NanoMesh"
  exit 1
fi

# Assimp OBJ UV pools: map 0 < numCoords < numVerts as SharedElements.
# Needed for first-class Content .obj assets (e.g. planet_sphere.obj).
# Kept as a StarDrive patch until it lands on gkapulis/NanoMesh.
NANOMESH_PATCH="${ROOT}/SDNative/patches/nanomesh-assimp-uv-sharedelements.patch"
if [[ -f "${NANOMESH_PATCH}" ]]; then
  if git -C "${ROOT}/SDNative/NanoMesh" apply --reverse --check "${NANOMESH_PATCH}" >/dev/null 2>&1; then
    : # already applied
  else
    git -C "${ROOT}/SDNative/NanoMesh" apply "${NANOMESH_PATCH}" \
      || echo "WARN: NanoMesh Assimp UV patch failed to apply (already applied or conflict)"
  fi
fi

if [[ "${ENABLE_FBX}" == "ON" ]]; then
  FBX_DYLIB="${ROOT}/SDNative/3rdparty/fbxsdk/macos/libfbxsdk.dylib"
  if [[ ! -f "${FBX_DYLIB}" ]]; then
    echo "==> Fetching Autodesk FBX SDK macOS runtime…"
    bash "${ROOT}/scripts/fetch-fbxsdk-macos.sh"
  fi
fi

if [[ "${ENABLE_FFMPEG}" == "ON" ]]; then
  FF_LIB="${ROOT}/SDNative/3rdparty/ffmpeg/macos/lib/libavformat.dylib"
  FF_DIR="${ROOT}/SDNative/3rdparty/ffmpeg/macos/lib"
  need_ffmpeg=0
  if [[ ! -f "${FF_LIB}" ]]; then
    need_ffmpeg=1
  elif find "${FF_DIR}" -name '*.dylib' -type f -exec otool -L {} + 2>/dev/null \
      | grep -E '/opt/homebrew/|/usr/local/opt/' >/dev/null; then
    echo "==> Vendored FFmpeg links Homebrew paths — rebuilding self-contained…"
    need_ffmpeg=1
  fi
  if [[ "${need_ffmpeg}" -eq 1 ]]; then
    echo "==> Building vendored LGPL FFmpeg (macOS)…"
    bash "${ROOT}/scripts/fetch-ffmpeg-macos.sh" --force
  fi
fi

mkdir -p "${BUILD_DIR}" "${OUT_DIR}"
cmake -S "${ROOT}/SDNative" -B "${BUILD_DIR}" \
  -DCMAKE_BUILD_TYPE=Release \
  -DSDNATIVE_ENABLE_FBX="${ENABLE_FBX}" \
  -DSDNATIVE_ENABLE_FFMPEG="${ENABLE_FFMPEG}" \
  -DCMAKE_CXX_STANDARD=20
JOBS="$(sysctl -n hw.ncpu 2>/dev/null || echo 4)"
cmake --build "${BUILD_DIR}" -j"${JOBS}"

cp -f "${BUILD_DIR}/libSDNative.dylib" "${OUT_DIR}/libSDNative.dylib"
if [[ -f "${ROOT}/SDNative/3rdparty/fbxsdk/macos/libfbxsdk.dylib" ]]; then
  cp -f "${ROOT}/SDNative/3rdparty/fbxsdk/macos/libfbxsdk.dylib" "${OUT_DIR}/libfbxsdk.dylib"
  codesign --force --sign - "${OUT_DIR}/libfbxsdk.dylib" 2>/dev/null || true
  echo "Installed ${OUT_DIR}/libfbxsdk.dylib"
fi
FF_DIR="${ROOT}/SDNative/3rdparty/ffmpeg/macos/lib"
if [[ -d "${FF_DIR}" ]]; then
  # Copy real dylibs + version symlinks so @rpath/libavcodec.61.dylib resolves.
  for f in "${FF_DIR}"/libavutil*.dylib "${FF_DIR}"/libavcodec*.dylib \
           "${FF_DIR}"/libavformat*.dylib "${FF_DIR}"/libswscale*.dylib \
           "${FF_DIR}"/libswresample*.dylib; do
    [[ -e "$f" ]] || continue
    cp -a "$f" "${OUT_DIR}/"
  done
  echo "Installed FFmpeg dylibs → ${OUT_DIR}"
fi
codesign --force --sign - "${OUT_DIR}/libSDNative.dylib" 2>/dev/null || true
echo "Installed ${OUT_DIR}/libSDNative.dylib"

# Player machines have no Homebrew — refuse to ship absolute /opt/homebrew deps.
bash "${ROOT}/scripts/macos-check-dylib-deps.sh" "${OUT_DIR}"
