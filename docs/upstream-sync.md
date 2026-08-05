# Upstream sync (personal fork)

This fork tracks [TeamStarDrive/StarDrive](https://github.com/TeamStarDrive/StarDrive) while carrying DesktopVK / Mac / Linux work on `cross-platform`.

## Remotes

```bash
git remote add upstream https://github.com/TeamStarDrive/StarDrive.git   # once
git remote -v
# origin   -> your fork
# upstream -> TeamStarDrive/StarDrive
```

## Sync cadence

```bash
git fetch upstream
git checkout cross-platform
git rebase upstream/main
# resolve conflicts — usually csproj / Platform / SDNative/CMakeLists / scripts
git push --force-with-lease origin cross-platform   # only on your fork branch
```

Prefer **rebase** so Mac-specific commits stay on top of community `main`.

## Do-not-revert hotspots

Keep these when merging upstream:

| Path | Why |
|------|-----|
| `build/StarDrive.Platform.*` | Dual-host TFM / package selection |
| `Ship_Game/Platform/**` | Facades |
| `SDNative/CMakeLists.txt` | Unix native build |
| `scripts/**` | Mac/Linux publish |
| `.github/workflows/cross-platform.yml` | Multi-OS CI |
| `#if STARDIVE_DESKTOPVK` / `STARDIVE_WINDOWSDX` gates | Dual compile |

## Upstream PR strategy

1. Land adapter / `#if` cleanups that do not require Mac packaging.
2. Keep DMG / notarization fork-only until TeamStarDrive wants them.
3. WindowsDX must stay green — never replace DirectX as the default community host.
