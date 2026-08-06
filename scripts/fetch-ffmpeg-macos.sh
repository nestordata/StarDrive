#!/usr/bin/env bash
# Build / refresh vendored LGPL FFmpeg shared libs for macOS arm64 (SDVideo).
# Output: SDNative/3rdparty/ffmpeg/macos/{lib,include}
#
# Usage: bash scripts/fetch-ffmpeg-macos.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="${ROOT}/SDNative/3rdparty/ffmpeg/macos"
VER="${FFMPEG_VERSION:-7.1.1}"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "ERROR: this script only runs on macOS" >&2
  exit 1
fi

if [[ -f "${DEST}/lib/libavformat.dylib" && -f "${DEST}/include/libavformat/avformat.h" && "${1:-}" != "--force" ]]; then
  echo "FFmpeg already vendored at ${DEST} (pass --force to rebuild)"
  exit 0
fi

TMP="$(mktemp -d /tmp/ffmpeg-lgpl.XXXXXX)"
cleanup() { rm -rf "$TMP"; }
trap cleanup EXIT

echo "==> Downloading FFmpeg ${VER}…"
curl -L --fail -o "$TMP/ffmpeg.tar.xz" "https://ffmpeg.org/releases/ffmpeg-${VER}.tar.xz"
tar -xJf "$TMP/ffmpeg.tar.xz" -C "$TMP"
cd "$TMP/ffmpeg-${VER}"

echo "==> Configuring LGPL shared build (no GPL/nonfree)…"
./configure \
  --prefix="${DEST}" \
  --enable-shared --disable-static \
  --disable-programs --disable-doc --disable-debug \
  --disable-gpl --disable-nonfree \
  --disable-network \
  --disable-avdevice --disable-avfilter --disable-postproc \
  --enable-videotoolbox \
  --extra-cflags="-mmacosx-version-min=11.0" \
  --extra-ldflags="-mmacosx-version-min=11.0"

JOBS="$(sysctl -n hw.ncpu 2>/dev/null || echo 4)"
make -j"${JOBS}"
rm -rf "${DEST}"
make install

echo "==> Rewriting dylib ids to @rpath…"
python3 - <<PY
import os, subprocess, pathlib
lib = pathlib.Path("${DEST}/lib")
dylibs = [p for p in lib.glob("*.dylib") if not p.is_symlink()]
for d in dylibs:
    subprocess.run(["install_name_tool", "-id", f"@rpath/{d.name}", str(d)], check=False)
    out = subprocess.check_output(["otool", "-L", str(d)], text=True)
    for line in out.splitlines()[1:]:
        dep = line.strip().split()[0]
        if "/3rdparty/ffmpeg/macos/lib/" in dep or dep.startswith(str(lib)):
            base = os.path.basename(dep)
            subprocess.run(["install_name_tool", "-change", dep, f"@rpath/{base}", str(d)], check=False)
    subprocess.run(["codesign", "--force", "--sign", "-", str(d)], check=False)
print("vendored", len(dylibs), "dylibs →", lib)
PY

# Drop static archives / pkgconfig to keep the tree lean for git
rm -f "${DEST}/lib/"*.a
rm -rf "${DEST}/lib/pkgconfig" "${DEST}/share" || true
du -sh "${DEST}"
echo "==> Vendored LGPL FFmpeg → ${DEST}"
