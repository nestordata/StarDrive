# Vendored FFmpeg (LGPL) for SDVideo

DesktopVK video playback uses FFmpeg inside `libSDNative` (`SDNative/video/SDVideo.*`).

## macOS arm64

Tree: `macos/{lib,include}` — shared LGPL build (no GPL/nonfree), VideoToolbox enabled.
X11/xcb are **disabled** so dylibs stay self-contained (no `/opt/homebrew` install names).

Refresh / rebuild:

```bash
bash scripts/fetch-ffmpeg-macos.sh          # skip if already present
bash scripts/fetch-ffmpeg-macos.sh --force  # rebuild
```

Runtime dylibs are copied next to `libSDNative.dylib` by `scripts/build-sdnative.sh` / `scripts/build-mac.sh`.
`scripts/macos-check-dylib-deps.sh` fails the build if any shipped dylib still references Homebrew.

## License

FFmpeg is licensed under **LGPL 2.1 or later** in this configuration (`--disable-gpl --disable-nonfree`). See [FFmpeg License](https://ffmpeg.org/legal.html). Offer corresponding source for the exact release used (`FFMPEG_VERSION` in the fetch script, default `7.1.1`).
