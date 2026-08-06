#!/usr/bin/env bash
# Fail if Mach-O dylibs reference absolute Homebrew / non-system library paths.
# Player builds must be self-contained (@rpath + Apple system libs only).
#
# Usage:
#   bash scripts/macos-check-dylib-deps.sh <dir-or-dylib> [more…]
set -euo pipefail

die() { echo "ERROR: $*" >&2; exit 1; }

[[ $# -ge 1 ]] || die "usage: $0 <dir-or-dylib> [more…]"

# Absolute install names that are OK on a clean player Mac.
is_allowed() {
  local dep="$1"
  case "${dep}" in
    @rpath/*|@loader_path/*|@executable_path/*) return 0 ;;
    /usr/lib/*|/System/*) return 0 ;;
    /Library/Frameworks/*|/System/Library/*) return 0 ;;
    *) return 1 ;;
  esac
}

bad=0
scan_dylib() {
  local dylib="$1"
  local line dep
  # otool -L prints one header per arch for fat binaries:
  #   /abs/path/foo.dylib:
  #   /abs/path/foo.dylib (architecture arm64):
  # Those are not LC_LOAD_DYLIB deps — skip any line ending with ':'.
  while IFS= read -r line; do
    [[ "${line}" == *: ]] && continue
    dep="$(awk '{print $1}' <<<"${line}")"
    [[ -n "${dep}" ]] || continue
    if ! is_allowed "${dep}"; then
      echo "  BAD  $(basename "${dylib}"): ${dep}"
      bad=1
    fi
  done < <(otool -L "${dylib}" 2>/dev/null)
}

scan_path() {
  local path="$1"
  if [[ -f "${path}" ]]; then
    case "${path}" in
      *.dylib|*.so|*.so.*) scan_dylib "${path}" ;;
      *) die "not a dylib: ${path}" ;;
    esac
    return
  fi
  [[ -d "${path}" ]] || die "not found: ${path}"
  local f
  while IFS= read -r -d '' f; do
    scan_dylib "${f}"
  done < <(find "${path}" -maxdepth 1 -type f \( -name '*.dylib' -o -name 'lib*.so' -o -name 'lib*.so.*' \) -print0)
}

echo "==> Checking dylib dependency self-containment…"
for arg in "$@"; do
  scan_path "${arg}"
done

if [[ "${bad}" -ne 0 ]]; then
  die "One or more dylibs link absolute non-system paths (e.g. Homebrew). Rebuild vendored FFmpeg (bash scripts/fetch-ffmpeg-macos.sh --force) and ensure libpng is statically linked on macOS."
fi
echo "==> Dylib deps OK (no Homebrew / absolute third-party paths)"
