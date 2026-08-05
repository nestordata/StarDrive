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

# OBJ sidecars for meshes — Mac libSDNative has NANOMESH_NO_FBX until Autodesk SDK.
if command -v assimp >/dev/null 2>&1; then
  log "Ensuring .obj sidecars for .fbx meshes (assimp)"
  bash "${ROOT}/scripts/convert-fbx-to-obj.sh" || log "WARN: FBX→OBJ conversion had failures"
else
  log "WARN: assimp not installed — ship .fbx will not load until Phase 5 FBX SDK or: brew install assimp && bash scripts/convert-fbx-to-obj.sh"
fi

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

# Ensure native lib from build-sdnative is present (publish may not copy it)
if [[ -f "${OUT}/libSDNative.dylib" ]]; then
  :
elif [[ -f "${ROOT}/game/libSDNative.dylib" ]]; then
  cp -f "${ROOT}/game/libSDNative.dylib" "${OUT}/libSDNative.dylib"
else
  die "libSDNative.dylib missing after native build"
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
mkdir -p "${APP}/Contents/MacOS" "${GAME}"

rsync -a --delete "${OUT}/" "${GAME}/"

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
# DMG
# -----------------------------------------------------------------------------
if [[ "${SKIP_DMG}" -eq 0 ]]; then
  log "Creating DMG"
  rm -f "${DMG}"
  hdiutil create -volname "StarDrive" -srcfolder "${APP}" -ov -format UDZO "${DMG}"
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
