#!/usr/bin/env bash
# Build a DesktopVK-safe Content overlay from a Windows GitHub patch ZIP.
#
# Extracts Content/ (+ Mods/), converts WMV→MP4 when present, rebakes Vulkan
# effects when .fx sources change, optionally refreshes FBX→OBJ sidecars.
# Never copies Windows binaries / runtimeconfig / managed host assemblies.
#
# Usage:
#   bash scripts/apply-win-patch-content.sh /path/to/WindowsPatch.zip \
#     --out artifacts/mac-content-overlay
#
#   bash scripts/apply-win-patch-content.sh /path/to/WindowsPatch.zip \
#     --apply-to "/Applications/StarDrive.app/Contents/Resources/game"
#
#   bash scripts/apply-win-patch-content.sh /path/to/extracted-or-zip \
#     --out artifacts/mac-content-overlay --with-fbx-obj
#
# Flags:
#   --out DIR          Write filtered overlay (Content/, Mods/) here
#   --apply-to DIR     Also rsync overlay into an install/game directory
#   --with-fbx-obj     Run convert-fbx-to-obj.sh on staged Content
#   --force-video      Re-encode mp4 even if sibling exists
#
# Requires (as needed): unzip, ffmpeg (for .wmv), mgfxc + python3 (for .fx),
# assimp (for --with-fbx-obj). See docs/cross-platform.md Path B.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC=""
OUT=""
APPLY_TO=""
WITH_FBX_OBJ=0
FORCE_VIDEO=0

die() { echo "ERROR: $*" >&2; exit 1; }
log() { echo "==> $*"; }

usage() {
  sed -n '2,28p' "$0" | sed 's/^# \{0,1\}//'
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --out) OUT="${2:-}"; shift 2 ;;
    --apply-to) APPLY_TO="${2:-}"; shift 2 ;;
    --with-fbx-obj) WITH_FBX_OBJ=1; shift ;;
    --force-video) FORCE_VIDEO=1; shift ;;
    -h|--help) usage ;;
    -*) die "unknown flag: $1" ;;
    *)
      [[ -z "$SRC" ]] || die "unexpected extra arg: $1"
      SRC="$1"
      shift
      ;;
  esac
done

[[ -n "$SRC" ]] || die "missing Windows patch ZIP or extracted directory"
[[ -n "$OUT" || -n "$APPLY_TO" ]] || die "need --out and/or --apply-to"
[[ -e "$SRC" ]] || die "not found: $SRC"

STAGING="$(mktemp -d "${TMPDIR:-/tmp}/stardrive-win-content.XXXXXX")"
trap 'rm -rf "${STAGING}"' EXIT

EXTRACTED="${STAGING}/extract"
mkdir -p "${EXTRACTED}"

if [[ -f "$SRC" ]]; then
  log "Unzipping $(basename "$SRC")"
  unzip -q -o "$SRC" -d "${EXTRACTED}"
elif [[ -d "$SRC" ]]; then
  log "Using extracted tree: $SRC"
  # Copy so we never mutate the caller's tree while converting.
  rsync -a --exclude '.DS_Store' "${SRC}/" "${EXTRACTED}/"
else
  die "SRC must be a .zip file or directory: $SRC"
fi

# Patch ZIPs sometimes nest a single top-level folder.
PATCH_ROOT="${EXTRACTED}"
entries=("${EXTRACTED}"/*)
if [[ ${#entries[@]} -eq 1 && -d "${entries[0]}" ]]; then
  # Prefer nested root when it looks like a game/patch tree.
  if [[ -d "${entries[0]}/Content" || -f "${entries[0]}/Release.DeleteFiles.txt" ]]; then
    PATCH_ROOT="${entries[0]}"
  fi
fi

[[ -d "${PATCH_ROOT}/Content" ]] || die "no Content/ in patch (looked under ${PATCH_ROOT})"

OVERLAY="${STAGING}/overlay"
mkdir -p "${OVERLAY}/Content"
log "Staging Content/"
rsync -a --exclude '.DS_Store' "${PATCH_ROOT}/Content/" "${OVERLAY}/Content/"

if [[ -d "${PATCH_ROOT}/Mods" ]]; then
  log "Staging Mods/"
  mkdir -p "${OVERLAY}/Mods"
  rsync -a --exclude '.DS_Store' "${PATCH_ROOT}/Mods/" "${OVERLAY}/Mods/"
fi

for root_txt in Credits.txt upgrade-url.txt; do
  if [[ -f "${PATCH_ROOT}/${root_txt}" ]]; then
    cp -f "${PATCH_ROOT}/${root_txt}" "${OVERLAY}/${root_txt}"
  fi
done

# --- Video: WMV → MP4 in overlay ---
VIDEO_DIR="${OVERLAY}/Content/Video"
if [[ -d "${VIDEO_DIR}" ]] && compgen -G "${VIDEO_DIR}/*.wmv" >/dev/null; then
  log "Converting Content/Video/*.wmv → .mp4"
  if [[ "$FORCE_VIDEO" -eq 1 ]]; then
    bash "${ROOT}/scripts/convert-wmv-to-mp4.sh" --force "${VIDEO_DIR}"
  else
    bash "${ROOT}/scripts/convert-wmv-to-mp4.sh" "${VIDEO_DIR}"
  fi
fi
# DesktopVK does not use WMV; drop from overlay so installs stay clean.
if [[ -d "${VIDEO_DIR}" ]]; then
  find "${VIDEO_DIR}" -type f \( -iname '*.wmv' \) -delete 2>/dev/null || true
fi

# --- Effects: rebake Vulkan when .fx present in staging ---
FX_STAGED=0
if compgen -G "${OVERLAY}/Content/Effects/*.fx" >/dev/null \
  || compgen -G "${OVERLAY}/Content/3DParticles/*.fx" >/dev/null; then
  FX_STAGED=1
fi

if [[ "$FX_STAGED" -eq 1 ]]; then
  log "Syncing .fx into repo Content and rebaking Vulkan MGFX"
  mkdir -p "${ROOT}/game/Content/Effects" "${ROOT}/game/Content/3DParticles"
  shopt -s nullglob
  fx_files=("${OVERLAY}/Content/Effects/"*.fx)
  if [[ ${#fx_files[@]} -gt 0 ]]; then
    cp -f "${fx_files[@]}" "${ROOT}/game/Content/Effects/"
    for fxh in "${OVERLAY}/Content/Effects/"*.fxh; do
      cp -f "$fxh" "${ROOT}/game/Content/Effects/"
    done
  fi
  particle_fx=("${OVERLAY}/Content/3DParticles/"*.fx)
  if [[ ${#particle_fx[@]} -gt 0 ]]; then
    cp -f "${particle_fx[@]}" "${ROOT}/game/Content/3DParticles/"
  fi
  shopt -u nullglob
  bash "${ROOT}/scripts/rebake-effects-vulkan.sh"
  mkdir -p "${OVERLAY}/Content/Effects/Vulkan" "${OVERLAY}/Content/3DParticles/Vulkan"
  if [[ -d "${ROOT}/game/Content/Effects/Vulkan" ]]; then
    rsync -a "${ROOT}/game/Content/Effects/Vulkan/" "${OVERLAY}/Content/Effects/Vulkan/"
  fi
  if [[ -d "${ROOT}/game/Content/3DParticles/Vulkan" ]]; then
    rsync -a "${ROOT}/game/Content/3DParticles/Vulkan/" "${OVERLAY}/Content/3DParticles/Vulkan/"
  fi
fi

# Strip DirectX effect siblings from overlay (keep Vulkan/ + leave .fx for repo commits).
strip_non_vulkan_shaders() {
  local tree="$1"
  [[ -d "$tree" ]] || return 0
  find "$tree" -type f ! -path '*/Vulkan/*' \
    \( -iname '*.mgfx' -o -iname '*.mgfxo' -o -iname '*.xnb' \) -delete 2>/dev/null || true
}
strip_non_vulkan_shaders "${OVERLAY}/Content/Effects"
strip_non_vulkan_shaders "${OVERLAY}/Content/3DParticles"

if [[ "$WITH_FBX_OBJ" -eq 1 ]]; then
  log "Refreshing FBX→OBJ sidecars under staged Content"
  bash "${ROOT}/scripts/convert-fbx-to-obj.sh" "${OVERLAY}/Content" || log "WARN: FBX→OBJ had failures"
fi

# Never ship Windows binaries if they snuck into Content/ (paranoia).
find "${OVERLAY}" -type f \( \
  -iname '*.exe' -o -iname 'SDNative.dll' -o -iname '*.runtimeconfig.json' \
  -o -iname '*.deps.json' -o -iname 'SharpDX*.dll' -o -iname 'NAudio*.dll' \
  \) -delete 2>/dev/null || true

if [[ -n "$OUT" ]]; then
  log "Writing overlay → ${OUT}"
  mkdir -p "${OUT}"
  rsync -a --delete "${OVERLAY}/" "${OUT}/"
fi

if [[ -n "$APPLY_TO" ]]; then
  [[ -d "$APPLY_TO" ]] || die "--apply-to directory missing: $APPLY_TO"
  log "Applying overlay → ${APPLY_TO}"
  mkdir -p "${APPLY_TO}/Content"
  rsync -a "${OVERLAY}/Content/" "${APPLY_TO}/Content/"
  if [[ -d "${OVERLAY}/Mods" ]]; then
    mkdir -p "${APPLY_TO}/Mods"
    rsync -a "${OVERLAY}/Mods/" "${APPLY_TO}/Mods/"
  fi
  for root_txt in Credits.txt upgrade-url.txt; do
    [[ -f "${OVERLAY}/${root_txt}" ]] && cp -f "${OVERLAY}/${root_txt}" "${APPLY_TO}/${root_txt}"
  done
fi

log "Done (DesktopVK content bridge)"
[[ -n "$OUT" ]] && echo "Overlay: ${OUT}"
[[ -n "$APPLY_TO" ]] && echo "Applied to: ${APPLY_TO}"
