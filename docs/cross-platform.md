# Cross-platform (DesktopVK) notes

## Hosts

| Host | TFM | MonoGame | Native | Audio | Video |
|------|-----|----------|--------|-------|-------|
| WindowsDX (default on Windows) | `net8.0-windows` | WindowsDX 3.8.1.303 | `SDNative.dll` + FBX | NAudio WASAPI | Media Foundation `.wmv` |
| DesktopVK (default on Mac/Linux) | `net8.0` | Native + Mac/Linux.Vulkan **3.8.5** | `libSDNative` + FBX + **FFmpeg SDVideo** | MonoGame/FAudio | FFmpeg H.264/AAC `.mp4` |

Override: `/p:StarDrivePlatform=DesktopVK` or `WindowsDX`.

## Local Mac build

One-shot (submodules, restore, SDNative, publish, `.app`, DMG):

```bash
bash scripts/build-mac.sh
# optional: bash scripts/build-mac.sh --skip-dmg --open
```

Outputs: `artifacts/osx-arm64/`, `artifacts/StarDrive.app`, `artifacts/StarDrive-mac-arm64.dmg`.

App / DMG icons use checked-in [`Icons/AppIcon.icns`](../Icons/AppIcon.icns) (from `Mars.ico`; regenerate with `bash scripts/macos-make-icns.sh`).

**Updater:** GitHub patch ZIPs are WindowsDX-only. DesktopVK skips the in-game AutoUpdate scan entirely (vanilla and mod DownloadSite) so a Windows patch cannot overwrite `StarDrive.runtimeconfig.json` / managed DLLs inside the `.app`. Mac/Linux updates = new DMG/tarball until platform-specific patch assets exist.

Spike: `tools/DesktopVkSpike` (see NOTES.md).

## Video (Phase 5b)

- Windows keeps MF + `Content/Video/*.wmv` (+ tiny `.xnb` pointers).
- DesktopVK loads sibling **`Content/Video/*.mp4`** via `SDVideo` in `libSDNative` (LGPL FFmpeg).
- Convert / refresh mp4s: `bash scripts/convert-wmv-to-mp4.sh`
- Vendor FFmpeg: `bash scripts/fetch-ffmpeg-macos.sh` → `SDNative/3rdparty/ffmpeg/macos/`
- Probe: `StarDriveGame.ProbeVideoBackend` opens `Loading 2.mp4`; sets `GlobalStats.VideoDisabled` only on failure.
- See [SDNative/3rdparty/ffmpeg/README.md](../SDNative/3rdparty/ffmpeg/README.md).

## FBX / meshes

- macOS DesktopVK links Autodesk FBX SDK **2020.3.7** (`SDNative/3rdparty/fbxsdk/macos/libfbxsdk.dylib`) — same NanoMesh `Mesh_Fbx` / `FbxToOpenGL` path as Windows.
- Fetch macOS runtime: `bash scripts/fetch-fbxsdk-macos.sh`
- Linux runtime: place `libfbxsdk.so` under `SDNative/3rdparty/fbxsdk/linux/` (`bash scripts/fetch-fbxsdk-linux.sh` documents the steps). Until present, Assimp `.obj` sidecars are the fallback.
- **Modders:** ship `.fbx` + sibling DDS textures (same layout as Windows). Mac/Linux with FBX enabled do **not** require `.obj` sidecars; keep `.obj` only if you care about Linux-without-FBX or offline Assimp fallback.

## Updater / install

- DesktopVK: AutoUpdate scan is **disabled** (Windows patch ZIPs brick self-contained Mac/Linux installs).
- WindowsDX: `AutoUpdateChecker` / `AutoPatcher` still use GitHub Releases; UAC `runas` when needed.
- Gatekeeper release signing: [mac-notarization.md](mac-notarization.md).
- Linux desktop entry helper: `bash scripts/install-linux-desktop.sh artifacts/linux-x64`.

## Remaining gaps / waivers

- Effect shaders: run `scripts/rebake-effects-vulkan.sh` when `mgfxc` is available; Texture2D/SamplerState need `register(tN)`/`register(sN)` for Vulkan Apply.
- VRAM headroom: DesktopVK uses system-RAM GC probe; DXGI VRAM probe waived ([MemoryPressure.DesktopVK.cs](../Ship_Game/Graphics/MemoryPressure.DesktopVK.cs)).
- Linux FBX + Linux FFmpeg trees: layout ready; binaries fetched/vendored per platform as builds are cut.
- Full visual parity (post-process / distortion / shadows) — track in [parity-phase5.md](parity-phase5.md) §5d.

See [parity-phase5.md](parity-phase5.md) and [upstream-sync.md](upstream-sync.md).
