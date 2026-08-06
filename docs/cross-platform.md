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

Spike: `tools/DesktopVkSpike` (see NOTES.md).

## DesktopVK updates from Windows patches

Do **not** checkout upstream patch branches into this fork to “generate” a Mac patch. Use the **published Windows GitHub Release ZIP** plus one of the paths below.

```text
Upstream / fork shipped a Windows patch ZIP?
│
├─ Only Content data (yaml, textures, fbx, audio, mods data)?
│    → Path A (automatic): player AutoUpdate — no maintainer action
│
├─ ZIP also has new/changed .wmv and/or Content/Effects/*.fx?
│    → Path B (one script): apply-win-patch-content.sh → overlay / commit / optional apply
│
└─ Need C# / libSDNative / DesktopVK code from that release?
     → Path C: cherry-pick/merge into fork → build-mac.sh (and publish-linux.sh if shipping Linux)
```

**Never** manually unzip a full Windows patch over `StarDrive.app` — that overwrites `StarDrive.runtimeconfig.json` / WindowsDX DLLs and bricks the install.

| Asset in Windows ZIP | Path A (AutoPatcher) | Path B (maintainer script) |
|----------------------|----------------------|----------------------------|
| yaml / textures / most Content | Copy | Copy into overlay |
| `.fbx` | Copy (Mac FBX SDK) | Copy; optional `--with-fbx-obj` |
| `Content/Video/*.wmv` | **Skip** (keep `.mp4`) | Convert → `.mp4`, drop `.wmv` |
| `Content/Effects/**` (non-Vulkan) | **Skip** | Rebake → `Effects/Vulkan/` |
| `Content/Effects/Vulkan/**` | Copy if present | Copy after rebake |
| `StarDrive.dll` / runtime / natives | **Skip** | Not in overlay |

### Path A — Player AutoUpdate (fully automatic)

**Zero maintainer work** for ordinary Content/data patches.

1. Prerequisites: DesktopVK build with content-safe `AutoPatcher`; `DownloadSite` in `game/Content/Globals.yaml` points at a repo hosting the Windows release ZIPs (TeamStarDrive or a fork mirror of the same assets).
2. Player: open main menu → AutoUpdate discovers the release → downloads the Windows ZIP → applies **allowlisted** paths only.
3. Automatically skipped: `StarDrive.dll` / `SDGraphics.dll` / `SDUtils.dll`, `*.exe`, `SDNative.dll`, `*.runtimeconfig.json`, `*.deps.json`, host/CoreCLR natives, FFmpeg/FBX dylibs, SharpDX/NAudio, `Content/Video/*.wmv`, non-`Vulkan/` under `Content/Effects/` and `Content/3DParticles/`.
4. Deletes: only `Release.DeleteFiles.txt` entries under `Content/` or `Mods/`.
5. After apply (or when the ZIP has no DesktopVK Content), writes `AppliedContentVersion.txt` in the game directory. Main-menu version and AutoUpdate use `max(assembly, stamp)` so the UI advances and the same release is not offered again (Windows `StarDrive.dll` is never installed on Mac/Linux).
6. Verify: game still launches; `StarDrive.runtimeconfig.json` unchanged; `AppliedContentVersion.txt` matches the release; menu version reflects the stamp; `blackbox.log` shows `DesktopVK skip (Windows/binary/unsafe)` for skipped binaries.

If the ZIP has no Content/Mods changes after filtering, AutoPatcher still stamps the version (“marked applied”) so AutoUpdate stops looping; C#/native fixes still need Path C.

### Path B — Maintainer content port (one command)

**When:** Windows ZIP adds/changes `Content/Video/*.wmv` or `Content/Effects/*.fx` (or you want Linux Assimp `.obj` sidecars refreshed). Path A still applies ordinary Content from the same ZIP to players; Path B produces Mac/Linux-native siblings the Windows ZIP does not contain.

**Tooling once:** `ffmpeg`, `dotnet tool install -g dotnet-mgfxc --version 3.8.5`, optional `assimp` (`--with-fbx-obj`).

```bash
# Download the Windows patch asset, then:
bash scripts/apply-win-patch-content.sh /path/to/WindowsPatch.zip \
  --out artifacts/mac-content-overlay

# Optional: apply into a local .app install
bash scripts/apply-win-patch-content.sh /path/to/WindowsPatch.zip \
  --apply-to "/Applications/StarDrive.app/Contents/Resources/game"

# Optional: also refresh FBX→OBJ sidecars for Linux
bash scripts/apply-win-patch-content.sh /path/to/WindowsPatch.zip \
  --out artifacts/mac-content-overlay --with-fbx-obj
```

Automated inside the script: extract `Content/` (+ `Mods/`) → convert WMV → rebake Vulkan when `.fx` present → strip DirectX `.mgfx`/`.xnb` siblings → write overlay. Flags/details: header of [`scripts/apply-win-patch-content.sh`](../scripts/apply-win-patch-content.sh).

After overlay: commit needed files under `game/Content` before the next DMG, copy into a local install via `--apply-to`, or zip the overlay for a rare fork-only Content drop.

Related helpers (invoked by the bridge or alone):

- [`scripts/convert-wmv-to-mp4.sh`](../scripts/convert-wmv-to-mp4.sh)
- [`scripts/rebake-effects-vulkan.sh`](../scripts/rebake-effects-vulkan.sh) / [`scripts/fx-to-vulkan.py`](../scripts/fx-to-vulkan.py)
- [`scripts/convert-fbx-to-obj.sh`](../scripts/convert-fbx-to-obj.sh)

### Path C — Full DesktopVK binary rebuild (semi-automated)

**When:** logic / `libSDNative` / DesktopVK platform fixes are required. Windows `StarDrive.dll` cannot be applied on Mac/Linux.

1. Cherry-pick specific upstream commits onto `cross-platform` (prefer not wholesale-replacing the fork branch).
2. Build:

```bash
bash scripts/build-mac.sh          # DMG + .app
bash scripts/publish-linux.sh      # when shipping Linux
```

3. Upload the new DMG / Linux publish to the fork Releases until upstream accepts DesktopVK.
4. After Path C, Path A remains the day-to-day Content channel.

## Video (Phase 5b)

- Windows keeps MF + `Content/Video/*.wmv` (+ tiny `.xnb` pointers).
- DesktopVK loads sibling **`Content/Video/*.mp4`** via `SDVideo` in `libSDNative` (LGPL FFmpeg).
- Convert / refresh mp4s: `bash scripts/convert-wmv-to-mp4.sh` (or Path B bridge above).
- Vendor FFmpeg: `bash scripts/fetch-ffmpeg-macos.sh` → `SDNative/3rdparty/ffmpeg/macos/`
- Probe: `StarDriveGame.ProbeVideoBackend` opens `Loading 2.mp4`; sets `GlobalStats.VideoDisabled` only on failure.
- See [SDNative/3rdparty/ffmpeg/README.md](../SDNative/3rdparty/ffmpeg/README.md).

## FBX / meshes

- macOS DesktopVK links Autodesk FBX SDK **2020.3.7** (`SDNative/3rdparty/fbxsdk/macos/libfbxsdk.dylib`) — same NanoMesh `Mesh_Fbx` / `FbxToOpenGL` path as Windows.
- Fetch macOS runtime: `bash scripts/fetch-fbxsdk-macos.sh`
- Linux runtime: place `libfbxsdk.so` under `SDNative/3rdparty/fbxsdk/linux/` (`bash scripts/fetch-fbxsdk-linux.sh` documents the steps). Until present, Assimp `.obj` sidecars are the fallback.
- **Modders:** ship `.fbx` + sibling DDS textures (same layout as Windows). Mac/Linux with FBX enabled do **not** require `.obj` sidecars; keep `.obj` only if you care about Linux-without-FBX or offline Assimp fallback.

## Updater / install

- DesktopVK: AutoUpdate downloads the same Windows GitHub ZIPs; `AutoPatcher` applies Content/Mods only (see Path A above).
- WindowsDX: full ZIP apply; UAC `runas` when needed.
- Gatekeeper release signing: [mac-notarization.md](mac-notarization.md).
- Linux desktop entry helper: `bash scripts/install-linux-desktop.sh artifacts/linux-x64`.

## Remaining gaps / waivers

- Effect shaders: run `scripts/rebake-effects-vulkan.sh` (or Path B) when `mgfxc` is available; Texture2D/SamplerState need `register(tN)`/`register(sN)` for Vulkan Apply.
- VRAM headroom: DesktopVK uses system-RAM GC probe; DXGI VRAM probe waived ([MemoryPressure.DesktopVK.cs](../Ship_Game/Graphics/MemoryPressure.DesktopVK.cs)).
- Linux FBX + Linux FFmpeg trees: layout ready; binaries fetched/vendored per platform as builds are cut.
- Full visual parity (post-process / distortion / shadows) — track in [parity-phase5.md](parity-phase5.md) §5d.

See [parity-phase5.md](parity-phase5.md) and [upstream-sync.md](upstream-sync.md).
