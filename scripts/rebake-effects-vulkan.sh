#!/usr/bin/env bash
# Rebake MonoGame effects for DesktopVK (Vulkan/SPIR-V).
# Leaves DirectX_11 .mgfx / .mgfxo under game/Content/Effects/ untouched.
#
# Requires: mgfxc (dotnet tool install -g dotnet-mgfxc --version 3.8.5)
# Vulkan profile uses DXC and does NOT need Wine on macOS.
#
# Usage:
#   bash scripts/rebake-effects-vulkan.sh           # bake all *.fx under Effects/
#   bash scripts/rebake-effects-vulkan.sh Simple    # bake one effect by base name
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FX_DIR="${ROOT}/game/Content/Effects"
PARTICLES_DIR="${ROOT}/game/Content/3DParticles"
OUT_VK="${FX_DIR}/Vulkan"
OUT_PARTICLES_VK="${PARTICLES_DIR}/Vulkan"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/stardrive-vk-fx.XXXXXX")"
trap 'rm -rf "${TMP_DIR}"' EXIT
mkdir -p "${OUT_VK}" "${OUT_PARTICLES_VK}"

export PATH="${HOME}/.dotnet/tools:/opt/homebrew/bin:/usr/local/bin:${PATH}"

if ! command -v mgfxc >/dev/null 2>&1; then
  echo "ERROR: mgfxc not found. Install with:" >&2
  echo "  dotnet tool install -g dotnet-mgfxc --version 3.8.5" >&2
  exit 1
fi

bake_one() {
  local fx="$1"
  local out_dir="$2"
  local base
  base="$(basename "$fx" .fx)"
  local out_mgfx="${out_dir}/${base}.mgfx"
  local out_mgfxo="${out_dir}/${base}.mgfxo"
  local tmp_fx="${TMP_DIR}/${base}.fx"
  local src_dir
  src_dir="$(dirname "$fx")"
  # Copy includes next to temp fx when present
  [[ -f "${FX_DIR}/Simple.fxh" ]] && cp -f "${FX_DIR}/Simple.fxh" "${TMP_DIR}/" 2>/dev/null || true
  local fxh="${src_dir}/${base}.fxh"
  [[ -f "$fxh" ]] && cp -f "$fxh" "${TMP_DIR}/"

  if [[ "$base" == "Simple" ]]; then
    # Simple.fx already has #if VULKAN branches — compile source directly.
    tmp_fx="$fx"
  else
    python3 "${ROOT}/scripts/fx-to-vulkan.py" "$fx" "$tmp_fx"
  fi

  echo "Compiling ${base}.fx -> Vulkan (${out_dir#"${ROOT}/"})"
  if (cd "$(dirname "$tmp_fx")" && mgfxc "$(basename "$tmp_fx")" "${out_mgfx}" /Profile:Vulkan); then
    cp -f "${out_mgfx}" "${out_mgfxo}"
    return 0
  fi
  echo "WARN: failed ${base}.fx" >&2
  return 1
}

FAILED=0
OK=0

if [[ $# -gt 0 ]]; then
  for name in "$@"; do
    if [[ -f "${FX_DIR}/${name}.fx" ]]; then
      fx="${FX_DIR}/${name}.fx"
      out="${OUT_VK}"
    elif [[ -f "${PARTICLES_DIR}/${name}.fx" ]]; then
      fx="${PARTICLES_DIR}/${name}.fx"
      out="${OUT_PARTICLES_VK}"
    else
      echo "ERROR: missing ${name}.fx under Effects/ or 3DParticles/" >&2
      exit 1
    fi
    if bake_one "$fx" "$out"; then OK=$((OK+1)); else FAILED=$((FAILED+1)); fi
  done
else
  shopt -s nullglob
  for fx in "${FX_DIR}"/*.fx; do
    if bake_one "$fx" "${OUT_VK}"; then OK=$((OK+1)); else FAILED=$((FAILED+1)); fi
  done
  for fx in "${PARTICLES_DIR}"/*.fx; do
    if bake_one "$fx" "${OUT_PARTICLES_VK}"; then OK=$((OK+1)); else FAILED=$((FAILED+1)); fi
  done
fi

echo "Done. Vulkan MGFX under Content/**/Vulkan (ok=${OK} failed=${FAILED})"
echo "DesktopVK loaders prefer this directory over DirectX siblings."
# Don't fail the whole Mac build if some complex effects still need hand fixes.
exit 0
