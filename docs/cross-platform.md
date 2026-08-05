# Cross-platform (DesktopVK) notes

## Hosts

| Host | TFM | MonoGame | Native | Audio |
|------|-----|----------|--------|-------|
| WindowsDX (default on Windows) | `net8.0-windows` | WindowsDX 3.8.1.303 | `SDNative.dll` + FBX | NAudio WASAPI |
| DesktopVK (default on Mac/Linux) | `net8.0` | Native + Mac/Linux.Vulkan **3.8.5** | `libSDNative` (no FBX until Phase 5) | MonoGame/FAudio |

Override: `/p:StarDrivePlatform=DesktopVK` or `WindowsDX`.

## Local Mac build

One-shot (submodules, restore, SDNative, publish, `.app`, DMG):

```bash
bash scripts/build-mac.sh
# optional: bash scripts/build-mac.sh --skip-dmg --open
```

Outputs: `artifacts/osx-arm64/`, `artifacts/StarDrive.app`, `artifacts/StarDrive-mac-arm64.dmg`.

Spike: `tools/DesktopVkSpike` (see NOTES.md).

## Known Phase 4 gaps

- Runtime Autodesk FBX disabled (`NANOMESH_NO_FBX`) — DesktopVK loads sibling `.obj` sidecars from `scripts/convert-fbx-to-obj.sh` (requires `assimp`). Skinned animation still needs Phase 5 FBX SDK.
- Videos disabled (`GlobalStats.VideoDisabled`)
- Auto-updater elevation is Windows-centric
- Effect shaders: run `scripts/rebake-effects-vulkan.sh` when `mgfxc` is available; Texture2D/SamplerState need `register(tN)`/`register(sN)` for Vulkan Apply

See [parity-phase5.md](parity-phase5.md) and [upstream-sync.md](upstream-sync.md).
