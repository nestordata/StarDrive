# Phase 5 — Full parity checklist

Phase 4 validates a playable Mac/Linux install. Phase 5 closes remaining gaps vs Windows Jupiter.

## 5a — Autodesk FBX on Mac/Linux

- [x] Obtain Autodesk FBX SDK for macOS arm64 (2020.3.7 universal → thin arm64 in `3rdparty/fbxsdk/macos/`)
- [x] CMake `-DSDNATIVE_ENABLE_FBX=ON` + `ENABLE_FBX_MESH_LOADER=1` when runtime present
- [x] MeshImporter / asteroid `.fbx` smoke green on Mac (Autodesk path; visual confirm in-game)
- [x] Document modder `.fbx` workflow ([cross-platform.md](cross-platform.md))
- [x] Linux FBX CMake wire + fetch instructions (`3rdparty/fbxsdk/linux/`, `scripts/fetch-fbxsdk-linux.sh`) — runtime binary still maintainer-supplied

## 5b — Video playback

- [x] Replace MF-only `VideoPlayer` path on DesktopVK ([`ScreenMediaPlayer`](../Ship_Game/GameScreens/ScreenMediaPlayer.cs) + `SdNativeVideoPlayer` / `SDVideo`)
- [x] Restore splash / diplomacy / codex videos (`.mp4` via FFmpeg; convert with `scripts/convert-wmv-to-mp4.sh`)
- [x] Remove DesktopVK force-`VideoDisabled` stub; real probe in [`StarDriveGame.ProbeVideoBackend`](../Ship_Game/GameScreens/StarDriveGame.cs)
- [x] Vendor LGPL FFmpeg macOS (`scripts/fetch-ffmpeg-macos.sh`, `SDNative/3rdparty/ffmpeg/`)
- [x] Looping + PCM → FAudio (`DynamicSoundEffectInstance`) on DesktopVK

## 5c — Updater / install UX

- [x] Mac/Linux in-app update path (no Windows `runas` — `AutoPatcher.NeedsElevation` false on DesktopVK)
- [x] Code signing + notarization notes ([mac-notarization.md](mac-notarization.md))
- [x] Linux desktop entry helper (`scripts/install-linux-desktop.sh`); AppImage noted as follow-up

## 5d — Visual / systems parity

- [x] Inventory DesktopVK stubs: VRAM DXGI waived (RAM probe); video restored; FBX on macOS
- [ ] Post-process, distortion, shadows, particles, fonts — continue playtest / fix list
- [ ] Fullscreen / DPI / MoltenVK quirks from wider playtesting
- [x] VRAM pressure equivalent documented (optimistic VRAM on DesktopVK)
- [ ] Combined Arms smoke test on Mac (manual)

## 5e — Acceptance

- [x] Docs updated ([cross-platform.md](cross-platform.md), this file)
- [ ] Parity matrix green (or consciously waived) after CA + visual playtest sign-off

Player-facing target: same campaign/skirmish/mod content, audio, video, FBX mods, and update flow as Windows Jupiter on this fork’s Mac/Linux builds.
