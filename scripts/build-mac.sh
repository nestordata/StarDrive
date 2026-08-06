#!/usr/bin/env bash
# One-shot macOS Apple Silicon (DesktopVK) build for local testing.
# Produces: artifacts/osx-arm64/, artifacts/StarDrive.app, artifacts/StarDrive-mac-arm64.dmg
#
# Usage:
#   bash scripts/build-mac.sh              # full build + .app + DMG
#   bash scripts/build-mac.sh --skip-dmg   # skip DMG (faster iterate)
#   bash scripts/build-mac.sh --open       # open .app when done
#   bash scripts/build-mac.sh --skip-dmg --open
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${ROOT}/artifacts/osx-arm64"
APP="${ROOT}/artifacts/StarDrive.app"
DMG="${ROOT}/artifacts/StarDrive-mac-arm64.dmg"
SKIP_DMG=0
DO_OPEN=0

for arg in "$@"; do
  case "$arg" in
    --skip-dmg) SKIP_DMG=1 ;;
    --open) DO_OPEN=1 ;;
    -h|--help)
      sed -n '2,12p' "$0"
      exit 0
      ;;
    *)
      echo "Unknown option: $arg (try --help)" >&2
      exit 1
      ;;
  esac
done

log() { printf '\n==> %s\n' "$*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

# -----------------------------------------------------------------------------
# Preflight
# -----------------------------------------------------------------------------
[[ "$(uname -s)" == "Darwin" ]] || die "This script only runs on macOS."

export PATH="${HOME}/.dotnet:/opt/homebrew/bin:/usr/local/bin:${PATH}"

command -v cmake >/dev/null || die "cmake not found. Install: brew install cmake"
command -v dotnet >/dev/null || die "dotnet not found. Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0"
xcode-select -p >/dev/null 2>&1 || die "Xcode Command Line Tools missing. Run: xcode-select --install"

DOTNET_VER="$(dotnet --version 2>/dev/null || true)"
[[ "${DOTNET_VER}" == 8.* ]] || echo "WARNING: expected .NET 8.x SDK, found '${DOTNET_VER:-none}' — continuing anyway"

ARCH="$(uname -m)"
RID="osx-arm64"
if [[ "${ARCH}" != "arm64" ]]; then
  echo "WARNING: host is ${ARCH}; still publishing ${RID} (Apple Silicon). Cross-compile may fail on Intel Macs."
fi

[[ -d "${ROOT}/game/Content" ]] || die "game/Content missing — checkout the full repo / content tree before building."

# -----------------------------------------------------------------------------
# Submodules (SDNative ReCpp + NanoMesh)
# -----------------------------------------------------------------------------
log "Ensuring SDNative submodules"
if [[ ! -f "${ROOT}/SDNative/ReCpp/src/rpp/strview.h" ]] || [[ ! -f "${ROOT}/SDNative/NanoMesh/src/Mesh.cpp" ]]; then
  git -C "${ROOT}" submodule update --init --recursive
fi
[[ -f "${ROOT}/SDNative/ReCpp/src/rpp/strview.h" ]] || die "SDNative/ReCpp submodule still missing after init"
[[ -f "${ROOT}/SDNative/NanoMesh/src/Mesh.cpp" ]] || die "SDNative/NanoMesh submodule still missing after init"

# -----------------------------------------------------------------------------
# Restore + compile managed (catches C# errors before native/package work)
# -----------------------------------------------------------------------------
# Vulkan MGFX (DirectX siblings under Content/Effects are wrong for DesktopVK)
if [[ ! -f "${ROOT}/game/Content/Effects/Vulkan/Simple.mgfx" ]]; then
  log "Baking Vulkan effects (missing Content/Effects/Vulkan/Simple.mgfx)"
  bash "${ROOT}/scripts/rebake-effects-vulkan.sh" Simple || die "Vulkan effect bake failed — install: dotnet tool install -g dotnet-mgfxc --version 3.8.5"
else
  log "Vulkan effects present (Content/Effects/Vulkan)"
fi

# macOS builds link Autodesk FBX 2020.3.7 (same NanoMesh Mesh_Fbx path as Windows).

log "dotnet restore + build (DesktopVK / ${RID})"
mkdir -p "${OUT}" "${ROOT}/artifacts"
dotnet restore "${ROOT}/StarDrive.csproj" -p:StarDrivePlatform=DesktopVK -r "${RID}"
dotnet build "${ROOT}/StarDrive.csproj" -c Release -p:StarDrivePlatform=DesktopVK -r "${RID}" --no-restore

# -----------------------------------------------------------------------------
# Native
# -----------------------------------------------------------------------------
log "Building libSDNative.dylib"
bash "${ROOT}/scripts/build-sdnative.sh" "${OUT}"

# -----------------------------------------------------------------------------
# Publish self-contained apphost
# -----------------------------------------------------------------------------
log "dotnet publish self-contained ${RID}"
dotnet publish "${ROOT}/StarDrive.csproj" \
  -c Release \
  -p:StarDrivePlatform=DesktopVK \
  -r "${RID}" \
  --self-contained true \
  -o "${OUT}"

# Ensure native libs from build-sdnative are present (publish may not copy them)
if [[ -f "${OUT}/libSDNative.dylib" ]]; then
  :
elif [[ -f "${ROOT}/game/libSDNative.dylib" ]]; then
  cp -f "${ROOT}/game/libSDNative.dylib" "${OUT}/libSDNative.dylib"
else
  die "libSDNative.dylib missing after native build"
fi
if [[ -f "${ROOT}/SDNative/3rdparty/fbxsdk/macos/libfbxsdk.dylib" ]]; then
  cp -f "${ROOT}/SDNative/3rdparty/fbxsdk/macos/libfbxsdk.dylib" "${OUT}/libfbxsdk.dylib"
elif [[ -f "${ROOT}/game/libfbxsdk.dylib" ]]; then
  cp -f "${ROOT}/game/libfbxsdk.dylib" "${OUT}/libfbxsdk.dylib"
else
  log "WARN: libfbxsdk.dylib missing — FBX meshes need: bash scripts/fetch-fbxsdk-macos.sh"
fi

# FFmpeg LGPL dylibs for SDVideo (build-sdnative copies them into OUT already; refresh if missing)
FF_DIR="${ROOT}/SDNative/3rdparty/ffmpeg/macos/lib"
if [[ -d "${FF_DIR}" ]]; then
  for f in "${FF_DIR}"/libavutil*.dylib "${FF_DIR}"/libavcodec*.dylib \
           "${FF_DIR}"/libavformat*.dylib "${FF_DIR}"/libswscale*.dylib \
           "${FF_DIR}"/libswresample*.dylib; do
    [[ -e "$f" ]] || continue
    cp -a "$f" "${OUT}/"
  done
elif ! ls "${OUT}"/libavformat*.dylib >/dev/null 2>&1; then
  log "WARN: FFmpeg dylibs missing — videos need: bash scripts/fetch-ffmpeg-macos.sh"
fi

# Prefer @executable_path for dylib lookup next to the apphost.
# install_name_tool invalidates the ad-hoc signature — re-sign immediately after.
if [[ -x "${OUT}/StarDrive" ]]; then
  install_name_tool -add_rpath @executable_path "${OUT}/StarDrive" 2>/dev/null || true
fi

log "Ad-hoc codesigning Mach-O in publish dir (before .app assemble)"
find "${OUT}" -maxdepth 1 \( -name '*.dylib' -o -name 'lib*.so*' \) -type f -print0 \
  | while IFS= read -r -d '' lib; do
      codesign --force --sign - --timestamp=none "${lib}" || true
    done
codesign --force --sign - --timestamp=none "${OUT}/StarDrive"
codesign --verify --verbose=2 "${OUT}/StarDrive" || die "publish-dir StarDrive signature invalid"

# -----------------------------------------------------------------------------
# Stage game data (Content is required; CWD-relative at runtime)
# -----------------------------------------------------------------------------
stage_tree() {
  local src="$1" dst="$2"
  [[ -e "${src}" ]] || return 0
  rm -rf "${dst}"
  # APFS clonefile when available (near-instant); else rsync
  if cp -Rc "${src}" "${dst}" 2>/dev/null; then
    return 0
  fi
  mkdir -p "${dst}"
  rsync -a "${src}/" "${dst}/"
}

log "Staging Content / Mods / data (~$(du -sh "${ROOT}/game/Content" | awk '{print $1}'))"
stage_tree "${ROOT}/game/Content" "${OUT}/Content"
stage_tree "${ROOT}/game/Mods" "${OUT}/Mods"
stage_tree "${ROOT}/game/LegacyMesh" "${OUT}/LegacyMesh"
for f in Credits.txt upgrade-url.txt; do
  [[ -f "${ROOT}/game/${f}" ]] && cp -f "${ROOT}/game/${f}" "${OUT}/${f}"
done

# -----------------------------------------------------------------------------
# .app bundle
# macOS codesign treats .NET PE .dll files as nested code, so the self-contained
# publish cannot live in Contents/MacOS. Layout:
#   MacOS/StarDrive              — tiny native launcher (only Mach-O here)
#   Resources/game/              — full publish + Content/Mods
# -----------------------------------------------------------------------------
log "Assembling StarDrive.app"
rm -rf "${APP}"
GAME="${APP}/Contents/Resources/game"
mkdir -p "${APP}/Contents/MacOS" "${APP}/Contents/Resources" "${GAME}"

rsync -a --delete "${OUT}/" "${GAME}/"

# Checked-in icns (regenerate with: bash scripts/macos-make-icns.sh Icons/AppIcon.icns)
ICNS="${ROOT}/Icons/AppIcon.icns"
[[ -f "${ICNS}" ]] || die "missing ${ICNS} — run: bash scripts/macos-make-icns.sh Icons/AppIcon.icns"
cp -f "${ICNS}" "${APP}/Contents/Resources/AppIcon.icns"

log "Compiling native .app launcher"
clang -O2 -arch arm64 \
  -o "${APP}/Contents/MacOS/StarDrive" \
  "${ROOT}/scripts/macos-launcher.c"
chmod +x "${APP}/Contents/MacOS/StarDrive"

cat > "${APP}/Contents/Info.plist" << 'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>StarDrive</string>
  <key>CFBundleDisplayName</key><string>StarDrive BlackBox</string>
  <key>CFBundleIdentifier</key><string>io.stardriveteam.blackbox</string>
  <key>CFBundleVersion</key><string>1.60.0</string>
  <key>CFBundleShortVersionString</key><string>1.60</string>
  <key>CFBundleExecutable</key><string>StarDrive</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

ENTITLEMENTS="${ROOT}/scripts/macos.entitlements"
log "Ad-hoc codesigning launcher + nested game Mach-O"
find "${GAME}" -name '*.dylib' -type f -print0 \
  | while IFS= read -r -d '' lib; do
      codesign --force --sign - --timestamp=none --entitlements "${ENTITLEMENTS}" "${lib}" || true
    done
codesign --force --sign - --timestamp=none --entitlements "${ENTITLEMENTS}" "${GAME}/StarDrive"
codesign --force --sign - --timestamp=none --entitlements "${ENTITLEMENTS}" "${APP}/Contents/MacOS/StarDrive"
codesign --force --sign - --timestamp=none --entitlements "${ENTITLEMENTS}" "${APP}"
xattr -cr "${APP}" 2>/dev/null || true
codesign --verify --verbose=2 "${APP}" || die ".app signature invalid"
codesign --verify --verbose=2 "${APP}/Contents/MacOS/StarDrive" || die "launcher signature invalid"

# -----------------------------------------------------------------------------
# DMG (volume icon = Mars / AppIcon.icns)
# -----------------------------------------------------------------------------
if [[ "${SKIP_DMG}" -eq 0 ]]; then
  log "Creating DMG with Mars volume icon"
  rm -f "${DMG}"
  DMG_RW="${ROOT}/artifacts/StarDrive-mac-rw.dmg"
  rm -f "${DMG_RW}"

  # Stage folder: .app + Applications symlink (standard install UX)
  STAGE="${ROOT}/artifacts/dmg-stage"
  rm -rf "${STAGE}"
  mkdir -p "${STAGE}"
  cp -R "${APP}" "${STAGE}/StarDrive.app"
  ln -sf /Applications "${STAGE}/Applications"

  hdiutil create -volname "StarDrive" -srcfolder "${STAGE}" -ov -format UDRW "${DMG_RW}"
  MOUNT_DIR="${ROOT}/artifacts/dmg-mount"
  rm -rf "${MOUNT_DIR}"
  mkdir -p "${MOUNT_DIR}"
  hdiutil attach -readwrite -noverify -noautoopen -mountpoint "${MOUNT_DIR}" "${DMG_RW}" \
    || die "failed to mount ${DMG_RW}"
  cp -f "${ICNS}" "${MOUNT_DIR}/.VolumeIcon.icns"
  if command -v SetFile >/dev/null 2>&1; then
    SetFile -c icnC "${MOUNT_DIR}/.VolumeIcon.icns" || true
    SetFile -a C "${MOUNT_DIR}" || true
  else
    # Best-effort Finder custom-icon bit; never fail the build (xattr may be missing).
    DMG_MOUNT="${MOUNT_DIR}" python3 <<'PY' || true
import os, sys
try:
    import xattr
except ImportError:
    print("WARN: python xattr module missing; volume icon file present but custom-icon flag unset")
    sys.exit(0)
mount = os.environ["DMG_MOUNT"]
fi = bytearray(32)
fi[8] = 0x04  # kHasCustomIcon
try:
    xattr.setxattr(mount, "com.apple.FinderInfo", bytes(fi), 0, 0)
except Exception as e:
    print("WARN: could not set FinderInfo custom-icon flag:", e)
PY
  fi
  sync
  hdiutil detach "${MOUNT_DIR}" -quiet || hdiutil detach "${MOUNT_DIR}" -force || true
  rmdir "${MOUNT_DIR}" 2>/dev/null || true
  hdiutil convert "${DMG_RW}" -format UDZO -imagekey zlib-level=9 -o "${DMG}"
  rm -f "${DMG_RW}"
  rm -rf "${STAGE}"

  # Best-effort: stamp the .dmg file itself in Finder with Mars (needs `fileicon` brew).
  if command -v fileicon >/dev/null 2>&1; then
    fileicon set "${DMG}" "${ICNS}" || true
  fi
else
  log "Skipping DMG (--skip-dmg)"
fi

# -----------------------------------------------------------------------------
# Summary
# -----------------------------------------------------------------------------
echo
echo "============================================================"
echo " macOS build ready"
echo "============================================================"
echo " Folders:  ${OUT}"
echo " App:      ${APP}"
[[ "${SKIP_DMG}" -eq 0 ]] && echo " DMG:      ${DMG}"
echo
echo " Run from terminal (logs visible):"
echo "   cd \"${OUT}\" && ./StarDrive"
echo " Or open the app:"
echo "   open \"${APP}\""
echo "============================================================"

if [[ "${DO_OPEN}" -eq 1 ]]; then
  log "Opening StarDrive.app"
  open "${APP}"
fi
