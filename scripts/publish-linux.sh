#!/usr/bin/env bash
# Publish self-contained linux-x64 tarball.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${ROOT}/artifacts/linux-x64"
TAR="${ROOT}/artifacts/StarDrive-linux-x64.tar.gz"

export PATH="${HOME}/.dotnet:${PATH}"
mkdir -p "${OUT}" "${ROOT}/artifacts"

bash "${ROOT}/scripts/build-sdnative.sh" "${OUT}"

dotnet publish "${ROOT}/StarDrive.csproj" \
  -c Release \
  -p:StarDrivePlatform=DesktopVK \
  -r linux-x64 \
  --self-contained true \
  -o "${OUT}"

if [[ ! -d "${OUT}/Content" && -d "${ROOT}/game/Content" ]]; then
  cp -a "${ROOT}/game/Content" "${OUT}/Content"
fi

tar -C "${ROOT}/artifacts" -czf "${TAR}" linux-x64
echo "Tarball: ${TAR}"
