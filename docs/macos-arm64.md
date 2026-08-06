# macOS Apple Silicon (DesktopVK)

This fork’s product host is **macOS arm64** (`osx-arm64`) via DesktopVK / MoltenVK. Community Windows remains WindowsDX.

## New machine bootstrap

```bash
git clone git@github.com:nestordata/StarDrive.git
cd StarDrive
git checkout macos-arm64
bash scripts/macos-dev-setup.sh
```

Installs Xcode CLT (if needed), Homebrew deps, .NET 8, `mgfxc`, submodules, FBX/FFmpeg, then smokes `build-mac.sh --skip-dmg`.

## Hosts

| Host | TFM | MonoGame | Native | Audio | Video |
|------|-----|----------|--------|-------|-------|
| WindowsDX (community default) | `net8.0-windows` | WindowsDX 3.8.1.303 | `SDNative.dll` + FBX | NAudio WASAPI | Media Foundation `.wmv` |
| DesktopVK (macOS arm64) | `net8.0` | Native + Mac.Vulkan **3.8.5** | `libSDNative` + FBX + **FFmpeg SDVideo** | MonoGame/FAudio | FFmpeg H.264/AAC `.mp4` |

Override: `/p:StarDrivePlatform=DesktopVK` or `WindowsDX`.

## Local Mac build

```bash
bash scripts/build-mac.sh
# optional: bash scripts/build-mac.sh --skip-dmg --open
```

Outputs: `artifacts/osx-arm64/`, `artifacts/StarDrive.app`, `artifacts/StarDrive-mac-arm64.dmg`.

App / DMG icons use checked-in [`Icons/AppIcon.icns`](../Icons/AppIcon.icns) (regenerate with `bash scripts/macos-make-icns.sh`).

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
     → Path C: cherry-pick/merge into macos-arm64 → build-mac.sh
```

**Never** manually unzip a full Windows patch over `StarDrive.app` — that overwrites `StarDrive.runtimeconfig.json` / WindowsDX DLLs and bricks the install.

| Asset in Windows ZIP | Path A (AutoPatcher) | Path B (maintainer script) |
|----------------------|----------------------|----------------------------|
| yaml / textures / most Content | Copy | Copy into overlay |
| `.fbx` | Copy (Autodesk FBX SDK) | Copy |
| `Content/Video/*.wmv` | **Skip** (keep `.mp4`) | Convert → `.mp4`, drop `.wmv` |
| `Content/Effects/**` (non-Vulkan) | **Skip** | Rebake → `Effects/Vulkan/` |
| `Content/Effects/Vulkan/**` | Copy if present | Copy after rebake |
| `StarDrive.dll` / runtime / natives | **Skip** | Not in overlay |

### Path A — Player AutoUpdate (fully automatic)

**Zero maintainer work** for ordinary Content/data patches.

1. Prerequisites: DesktopVK build with content-safe `AutoPatcher`; `DownloadSite` in `game/Content/Globals.yaml` points at a repo hosting the Windows release ZIPs.
2. Player: open main menu → AutoUpdate → downloads the Windows ZIP → applies **allowlisted** paths only.
3. Skipped: Windows managed assemblies, exe, `runtimeconfig`/`deps`, natives, `Content/Video/*.wmv`, non-`Vulkan/` effects.
4. Deletes: only under `Content/` or `Mods/`.
5. Writes `AppliedContentVersion.txt`; menu version and AutoUpdate use `max(assembly, stamp)`.
6. Verify: launch OK; `runtimeconfig` unchanged; stamp matches release; log shows DesktopVK skips for binaries.

### Path B — Maintainer content port (one command)

**When:** Windows ZIP adds/changes `Content/Video/*.wmv` or `Content/Effects/*.fx`.

**Tooling once:** `ffmpeg`, `dotnet tool install -g dotnet-mgfxc --version 3.8.5`.

```bash
bash scripts/apply-win-patch-content.sh /path/to/WindowsPatch.zip \
  --out artifacts/mac-content-overlay

bash scripts/apply-win-patch-content.sh /path/to/WindowsPatch.zip \
  --apply-to "/Applications/StarDrive.app/Contents/Resources/game"
```

Helpers: [`convert-wmv-to-mp4.sh`](../scripts/convert-wmv-to-mp4.sh), [`rebake-effects-vulkan.sh`](../scripts/rebake-effects-vulkan.sh), [`fx-to-vulkan.py`](../scripts/fx-to-vulkan.py).

### Path C — Full DesktopVK binary rebuild

**When:** C# / `libSDNative` / DesktopVK code must ship. Windows `StarDrive.dll` cannot be applied on Mac.

```bash
bash scripts/build-mac.sh
```

Upload the DMG to the fork Releases until upstream accepts DesktopVK. Path A remains the day-to-day Content channel.

## Video

- Windows: MF + `Content/Video/*.wmv`
- Mac: sibling **`Content/Video/*.mp4`** via `SDVideo` / LGPL FFmpeg
- Convert: `bash scripts/convert-wmv-to-mp4.sh`
- Vendor: `bash scripts/fetch-ffmpeg-macos.sh`

## FBX / meshes

- Autodesk FBX SDK **2020.3.7** (`SDNative/3rdparty/fbxsdk/macos/libfbxsdk.dylib`) — same NanoMesh `Mesh_Fbx` path as Windows
- Fetch: `bash scripts/fetch-fbxsdk-macos.sh`
- **Modders:** ship `.fbx` + sibling DDS textures (same as Windows). First-class Content `.obj` assets still load; FBX→OBJ Assimp sidecars are not used.

## Updater / install

- DesktopVK: content-bridge AutoUpdate (Path A)
- WindowsDX: full ZIP apply; UAC when needed
- Gatekeeper: [mac-notarization.md](mac-notarization.md)

## Remaining gaps / waivers

- Effects: `scripts/rebake-effects-vulkan.sh` when `mgfxc` available
- VRAM: system-RAM GC probe; DXGI VRAM waived ([MemoryPressure.DesktopVK.cs](../Ship_Game/Graphics/MemoryPressure.DesktopVK.cs))
- Visual parity — [parity-phase5.md](parity-phase5.md) §5d

See [parity-phase5.md](parity-phase5.md) and [upstream-sync.md](upstream-sync.md).
