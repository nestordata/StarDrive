#!/usr/bin/env bash
# Convert game/Content/Video/*.wmv → sibling H.264/AAC .mp4 for DesktopVK FFmpeg playback.
# WindowsDX continues to use .wmv + Media Foundation.
#
# Usage: bash scripts/convert-wmv-to-mp4.sh [--force]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VIDEO_DIR="${ROOT}/game/Content/Video"
FORCE=0
[[ "${1:-}" == "--force" ]] && FORCE=1

if ! command -v ffmpeg >/dev/null 2>&1; then
  echo "ERROR: ffmpeg CLI not found (brew install ffmpeg)" >&2
  exit 1
fi

shopt -s nullglob
count=0
for wmv in "${VIDEO_DIR}"/*.wmv; do
  base="$(basename "$wmv" .wmv)"
  mp4="${VIDEO_DIR}/${base}.mp4"
  if [[ -f "$mp4" && "$FORCE" -eq 0 ]]; then
    echo "skip (exists): ${base}.mp4"
    continue
  fi
  echo "==> ${base}.wmv → ${base}.mp4"
  # yuv420p + aac for broad decoder support; -movflags +faststart for progressive load.
  ffmpeg -y -hide_banner -loglevel error -i "$wmv" \
    -c:v libx264 -pix_fmt yuv420p -preset medium -crf 20 \
    -c:a aac -b:a 160k \
    -movflags +faststart \
    "$mp4"
  count=$((count + 1))
done
echo "Converted ${count} video(s) → ${VIDEO_DIR}"
