#!/usr/bin/env bash
# Back-compat entry point — full pipeline lives in build-mac.sh
exec "$(cd "$(dirname "$0")" && pwd)/build-mac.sh" "$@"
