#!/usr/bin/env bash
# Bootstrap a macOS Apple Silicon machine for StarDrive DesktopVK development.
#
#   git clone git@github.com:nestordata/StarDrive.git
#   cd StarDrive
#   git checkout macos-arm64
#   bash scripts/macos-dev-setup.sh
#
# Idempotent: safe to re-run. Ends with build-mac.sh --skip-dmg smoke build.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "${ROOT}"

die() { echo "ERROR: $*" >&2; exit 1; }
log() { echo "==> $*"; }

[[ "$(uname -s)" == "Darwin" ]] || die "This script only runs on macOS."
[[ "$(uname -m)" == "arm64" ]] || die "Apple Silicon (arm64) required; got $(uname -m)."

export PATH="${HOME}/.dotnet:${HOME}/.dotnet/tools:/opt/homebrew/bin:/usr/local/bin:${PATH}"

# --- Xcode CLT ---
if ! xcode-select -p >/dev/null 2>&1; then
  log "Installing Xcode Command Line Tools (GUI prompt)…"
  xcode-select --install || true
  echo "Finish the CLT installer, then re-run: bash scripts/macos-dev-setup.sh"
  exit 1
fi
log "Xcode CLT: $(xcode-select -p)"

# --- Homebrew ---
if ! command -v brew >/dev/null 2>&1; then
  log "Installing Homebrew…"
  /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
  # Apple Silicon brew lives under /opt/homebrew
  if [[ -x /opt/homebrew/bin/brew ]]; then
    eval "$(/opt/homebrew/bin/brew shellenv)"
  fi
fi
command -v brew >/dev/null 2>&1 || die "brew not on PATH after install"
log "Homebrew: $(brew --prefix)"

log "brew install cmake pkg-config python3 ffmpeg"
brew install cmake pkg-config python3 ffmpeg

# --- .NET 8 SDK ---
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  log "Installing .NET 8 SDK…"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 8.0
  export PATH="${HOME}/.dotnet:${HOME}/.dotnet/tools:${PATH}"
fi
command -v dotnet >/dev/null 2>&1 || die "dotnet not on PATH"
log "dotnet: $(dotnet --version)"

# --- mgfxc (Vulkan effects) ---
if ! command -v mgfxc >/dev/null 2>&1; then
  log "Installing dotnet-mgfxc 3.8.5…"
  dotnet tool install -g dotnet-mgfxc --version 3.8.5
else
  log "mgfxc already present: $(command -v mgfxc)"
fi

# --- Submodules ---
log "git submodule update --init --recursive"
git submodule update --init --recursive

# --- Vendored natives (also auto-fetched by build-sdnative if missing) ---
if [[ ! -f "${ROOT}/SDNative/3rdparty/fbxsdk/macos/libfbxsdk.dylib" ]]; then
  log "Fetching Autodesk FBX SDK macOS runtime…"
  bash "${ROOT}/scripts/fetch-fbxsdk-macos.sh"
else
  log "FBX SDK already vendored"
fi

if [[ ! -f "${ROOT}/SDNative/3rdparty/ffmpeg/macos/lib/libavformat.dylib" ]]; then
  log "Building vendored LGPL FFmpeg (macOS) — may take several minutes…"
  bash "${ROOT}/scripts/fetch-ffmpeg-macos.sh"
else
  log "FFmpeg already vendored"
fi

# --- Smoke build ---
log "Smoke: bash scripts/build-mac.sh --skip-dmg"
bash "${ROOT}/scripts/build-mac.sh" --skip-dmg

echo ""
echo "========================================"
echo " macOS Apple Silicon DesktopVK ready"
echo "========================================"
echo " Branch tip: macos-arm64"
echo " App:        ${ROOT}/artifacts/StarDrive.app"
echo ""
echo " Next:"
echo "   bash scripts/build-mac.sh --open     # rebuild + launch"
echo "   bash scripts/build-mac.sh            # also make DMG"
echo " Docs: docs/macos-arm64.md"
echo ""
