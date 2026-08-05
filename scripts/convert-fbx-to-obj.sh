#!/usr/bin/env bash
# Optional: Content/**/*.fbx → sibling .obj/.mtl fallback when FBX SDK is off
# (e.g. Linux). macOS DesktopVK links Autodesk FBX; these sidecars are unused
# unless SDMeshOpen(.fbx) fails. Requires: assimp, python3.
#
# Assimp export ≠ NanoMesh FBX: needs FbxToOpenGL remap and undo FlipUVs.
# scripts/fbx_obj_postprocess.py remaps axes, un-flips V, rewrites MTL maps.
#
# Usage:
#   bash scripts/convert-fbx-to-obj.sh              # all under game/Content
#   bash scripts/convert-fbx-to-obj.sh path/to/dir  # only under dir
#   FORCE=1 bash scripts/convert-fbx-to-obj.sh      # ignore up-to-date skip
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONTENT="${1:-${ROOT}/game/Content}"
POST="${ROOT}/scripts/fbx_obj_postprocess.py"
FORCE="${FORCE:-0}"

if ! command -v assimp >/dev/null 2>&1; then
  echo "ERROR: assimp not found. Install with: brew install assimp" >&2
  exit 1
fi
if ! command -v python3 >/dev/null 2>&1; then
  echo "ERROR: python3 not found" >&2
  exit 1
fi

OK=0
SKIP=0
FAIL=0
while IFS= read -r -d '' fbx; do
  base="$fbx"
  case "$base" in
    *.fbx) base="${base%.fbx}" ;;
    *.FBX) base="${base%.FBX}" ;;
  esac
  obj="${base}.obj"
  mtl="${base}.mtl"

  if [[ "$FORCE" != "1" && -f "$obj" && "$obj" -nt "$fbx" && "$obj" -nt "$POST" \
        && -f "$mtl" ]] && grep -q 'map_Kd' "$mtl" 2>/dev/null; then
    SKIP=$((SKIP+1))
    continue
  fi

  if assimp export "$fbx" "$obj" >/dev/null 2>&1; then
    if python3 "$POST" "$obj" --fbx "$fbx"; then
      OK=$((OK+1))
      echo "OK  ${obj#"${ROOT}/"}"
    else
      FAIL=$((FAIL+1))
      echo "FAIL post ${obj#"${ROOT}/"}" >&2
    fi
  else
    FAIL=$((FAIL+1))
    echo "FAIL ${fbx#"${ROOT}/"}" >&2
  fi
done < <(find "$CONTENT" \( -name '*.fbx' -o -name '*.FBX' \) -print0)

echo "Done. converted=${OK} skipped_up_to_date=${SKIP} failed=${FAIL}"
exit 0
