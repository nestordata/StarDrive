# Phase 5 — Full parity checklist

Phase 4 validates a playable Mac/Linux install. Phase 5 closes remaining gaps vs Windows Jupiter.

## 5a — Autodesk FBX on Mac/Linux

- [ ] Obtain Autodesk FBX SDK for macOS arm64 + Linux
- [ ] CMake `-DSDNATIVE_ENABLE_FBX=ON` and drop `NANOMESH_NO_FBX`
- [ ] MeshImporter / asteroid `.fbx` tests green on Mac
- [ ] Document modder `.fbx` workflow

## 5b — Video playback

- [ ] Replace MF-only `VideoPlayer` path ([`ScreenMediaPlayer`](../Ship_Game/GameScreens/ScreenMediaPlayer.cs))
- [ ] Restore splash / diplomacy videos
- [ ] Remove DesktopVK `VideoDisabled` stub in [`StarDriveGame.ProbeVideoBackend`](../Ship_Game/GameScreens/StarDriveGame.cs)

## 5c — Updater / install UX

- [ ] Mac/Linux in-app update path (no Windows `runas`)
- [ ] Code signing + notarization for Gatekeeper DMGs
- [ ] Linux desktop entry / AppImage polish

## 5d — Visual / systems parity

- [ ] Post-process, distortion, shadows, particles, fonts
- [ ] Fullscreen / DPI / MoltenVK quirks
- [ ] VRAM pressure equivalent ([`MemoryPressure`](../Ship_Game/Graphics/MemoryPressure.cs))
- [ ] Combined Arms smoke test on Mac

## 5e — Acceptance

Parity matrix green (or consciously waived). Player-facing: same campaign/skirmish/mod content, audio, video, FBX mods, and update flow as Windows Jupiter on this fork’s Mac/Linux builds.
