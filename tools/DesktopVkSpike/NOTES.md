# DesktopVK spike notes (Phase 0)

Validated on Apple Silicon (M1) with:

- .NET 8 SDK (`osx-arm64`)
- `MonoGame.Framework.Native` **3.8.5**
- `MonoGame.Runtime.Mac.Vulkan` **3.8.5**
- `<MonoGamePlatform>DesktopVK</MonoGamePlatform>`

## Run

```bash
export PATH="$HOME/.dotnet:$PATH"
cd tools/DesktopVkSpike
dotnet run -r osx-arm64 -c Release
```

## Observed runtime

- Vulkan instance via MoltenVK (`VK_MVK_moltenvk`, `VK_EXT_metal_surface`)
- Selected GPU: Apple M1
- Native runtime: `libmgruntime.dylib` (bundled by the Mac.Vulkan package)

## Package set for DesktopVK hosts

```xml
<MonoGamePlatform>DesktopVK</MonoGamePlatform>
<PackageReference Include="MonoGame.Framework.Native" Version="3.8.5" />
<!-- Pick the runtime package(s) for the OS you publish: -->
<PackageReference Include="MonoGame.Runtime.Mac.Vulkan" Version="3.8.5" />
<PackageReference Include="MonoGame.Runtime.Linux.Vulkan" Version="3.8.5" />
<!-- Optional on Windows if experimenting with DesktopVK there: -->
<PackageReference Include="MonoGame.Runtime.Windows.Vulkan" Version="3.8.5" />
```

Windows community builds stay on `MonoGame.Framework.WindowsDX`.
