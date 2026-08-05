#if STARDIVE_DESKTOPVK
using Microsoft.Xna.Framework.Graphics;

namespace Ship_Game.Graphics;

/// <summary>
/// DesktopVK: DXGI VRAM probes unavailable. System RAM probe still used; VRAM assumed OK.
/// </summary>
public static class MemoryPressure
{
    public const double MinSystemRamHeadroomMB = 2048;
    public const double MinVramHeadroomMB = 1024;

    public static bool HasHeadroomToKeepContent(GraphicsDevice device)
    {
        _ = device;
        bool ramOk = TryGetSystemRamHeadroomMB(out double ramFreeMB) && ramFreeMB >= MinSystemRamHeadroomMB;
        Log.Info($"ExitToMain memory check (DesktopVK): RAM free={ramFreeMB:0}MB ok={ramOk}; VRAM probe skipped");
        return ramOk;
    }

    public static bool TryGetSystemRamHeadroomMB(out double freeMB)
    {
        try
        {
            // GC.GetGCMemoryInfo available on net8 — use total available as approximation.
            var info = System.GC.GetGCMemoryInfo();
            freeMB = (info.TotalAvailableMemoryBytes - info.MemoryLoadBytes) / (1024.0 * 1024.0);
            if (freeMB < 0) freeMB = 0;
            return true;
        }
        catch
        {
            freeMB = 0;
            return false;
        }
    }

    public static bool TryGetVramHeadroomMB(GraphicsDevice device, out double freeMB)
    {
        _ = device;
        freeMB = MinVramHeadroomMB; // optimistic — no DXGI on DesktopVK
        return true;
    }
}
#endif
