#!/usr/bin/env bash
# Convert Content/**/*.fbx → sibling .obj/.mtl for DesktopVK (NANOMESH_NO_FBX).
# Requires: assimp (brew install assimp)
#
# Usage:
#   bash scripts/convert-fbx-to-obj.sh              # all under game/Content
#   bash scripts/convert-fbx-to-obj.sh path/to/dir  # only under dir
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONTENT="${1:-${ROOT}/game/Content}"

if ! command -v assimp >/dev/null 2>&1; then
  echo "ERROR: assimp not found. Install with: brew install assimp" >&2
  exit 1
fi

OK=0
SKIP=0
FAIL=0
while IFS= read -r -d '' fbx; do
  # Strip .fbx / .FBX once, then append .obj
  base="$fbx"
  case "$base" in
    *.fbx) base="${base%.fbx}" ;;
    *.FBX) base="${base%.FBX}" ;;
  esac
  obj="${base}.obj"
  if [[ -f "$obj" && "$obj" -nt "$fbx" ]]; then
    SKIP=$((SKIP+1))
    continue
  fi
  if assimp export "$fbx" "$obj" >/dev/null 2>&1; then
    OK=$((OK+1))
    echo "OK  ${obj#"${ROOT}/"}"
  else
    FAIL=$((FAIL+1))
    echo "FAIL ${fbx#"${ROOT}/"}" >&2
  fi
done < <(find "$CONTENT" \( -name '*.fbx' -o -name '*.FBX' \) -print0)

echo "Done. converted=${OK} skipped_up_to_date=${SKIP} failed=${FAIL}"
exit 0
