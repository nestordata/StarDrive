# Vendored FFmpeg (LGPL) for SDVideo

DesktopVK video playback uses FFmpeg inside `libSDNative` (`SDNative/video/SDVideo.*`).

## macOS arm64

Tree: `macos/{lib,include}` — shared LGPL build (no GPL/nonfree), VideoToolbox enabled.

Refresh / rebuild:

```bash
bash scripts/fetch-ffmpeg-macos.sh          # skip if already present
bash scripts/fetch-ffmpeg-macos.sh --force  # rebuild
```

Runtime dylibs are copied next to `libSDNative.dylib` by `scripts/build-sdnative.sh` / `scripts/build-mac.sh`.

## Linux x64

Place an equivalent LGPL shared install under `linux/{lib,include}` (same layout). CMake enables FFmpeg when `linux/include/libavformat/avformat.h` exists. A dedicated `fetch-ffmpeg-linux.sh` can be added when packaging Linux builds.

## License

FFmpeg is licensed under **LGPL 2.1 or later** in this configuration (`--disable-gpl --disable-nonfree`). See [FFmpeg License](https://ffmpeg.org/legal.html). Offer corresponding source for the exact release used (`FFMPEG_VERSION` in the fetch script, default `7.1.1`).
