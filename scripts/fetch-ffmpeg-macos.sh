#!/usr/bin/env bash
# Build / refresh vendored LGPL FFmpeg shared libs for macOS arm64 (SDVideo).
# Output: SDNative/3rdparty/ffmpeg/macos/{lib,include}
#
# Player builds must be self-contained: no absolute Homebrew (or other
# third-party) install names. X11/xcb are disabled — VideoToolbox only.
#
# Usage: bash scripts/fetch-ffmpeg-macos.sh
#        bash scripts/fetch-ffmpeg-macos.sh --force
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

echo "==> Configuring LGPL shared build (no GPL/nonfree, no X11)…"
# Isolate from Homebrew pkg-config so configure cannot auto-enable xlib/xcb/etc.
export PKG_CONFIG_PATH=""
export PKG_CONFIG_LIBDIR="/usr/lib/pkgconfig"
./configure \
  --prefix="${DEST}" \
  --enable-shared --disable-static \
  --disable-programs --disable-doc --disable-debug \
  --disable-gpl --disable-nonfree \
  --disable-network \
  --disable-avdevice --disable-avfilter --disable-postproc \
  --disable-xlib --disable-libxcb \
  --disable-libxcb-shm --disable-libxcb-xfixes --disable-libxcb-shape \
  --enable-videotoolbox \
  --extra-cflags="-mmacosx-version-min=11.0" \
  --extra-ldflags="-mmacosx-version-min=11.0"

JOBS="$(sysctl -n hw.ncpu 2>/dev/null || echo 4)"
make -j"${JOBS}"
rm -rf "${DEST}"
make install

echo "==> Rewriting dylib ids to @rpath + auditing deps…"
export DEST
python3 - <<'PY'
import os, subprocess, sys, pathlib

lib = pathlib.Path(os.environ["DEST"]) / "lib"
dylibs = [p for p in lib.glob("*.dylib") if not p.is_symlink()]

def allowed(dep: str) -> bool:
    return (
        dep.startswith("@rpath/")
        or dep.startswith("@loader_path/")
        or dep.startswith("@executable_path/")
        or dep.startswith("/usr/lib/")
        or dep.startswith("/System/")
    )

bad = []
for d in dylibs:
    subprocess.run(["install_name_tool", "-id", f"@rpath/{d.name}", str(d)], check=False)
    out = subprocess.check_output(["otool", "-L", str(d)], text=True)
    for line in out.splitlines()[1:]:
        dep = line.strip().split()[0]
        if "/3rdparty/ffmpeg/macos/lib/" in dep or dep.startswith(str(lib)):
            base = os.path.basename(dep)
            subprocess.run(["install_name_tool", "-change", dep, f"@rpath/{base}", str(d)], check=False)
    # Re-read after rewrite and audit
    out = subprocess.check_output(["otool", "-L", str(d)], text=True)
    for line in out.splitlines()[1:]:
        dep = line.strip().split()[0]
        if not allowed(dep):
            bad.append(f"{d.name}: {dep}")
    subprocess.run(["codesign", "--force", "--sign", "-", str(d)], check=False)

if bad:
    print("ERROR: vendored FFmpeg still has non-system absolute deps:", file=sys.stderr)
    for b in bad:
        print(f"  {b}", file=sys.stderr)
    print("Configure must not link Homebrew (X11/xcb/etc).", file=sys.stderr)
    sys.exit(1)

print("vendored", len(dylibs), "dylibs →", lib)
PY

# Drop static archives / pkgconfig to keep the tree lean for git
rm -f "${DEST}/lib/"*.a
rm -rf "${DEST}/lib/pkgconfig" "${DEST}/share" || true
du -sh "${DEST}"
echo "==> Vendored LGPL FFmpeg → ${DEST}"
