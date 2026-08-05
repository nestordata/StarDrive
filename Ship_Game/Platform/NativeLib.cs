using System;
using System.Runtime.InteropServices;

namespace Ship_Game.Platform;

/// <summary>
/// Resolves SDNative shared-library name and calling convention across hosts.
/// Windows keeps historical SDNative.dll + stdcall; Unix uses libSDNative + cdecl.
/// </summary>
public static class NativeLib
{
#if STARDIVE_WINDOWSDX || WINDOWS
    public const string Name = "SDNative.dll";
    public const CallingConvention CallConv = CallingConvention.StdCall;
#else
    // dlopen on macOS/Linux resolves "libSDNative" / "SDNative" via the usual prefixes.
    public const string Name = "SDNative";
    public const CallingConvention CallConv = CallingConvention.Cdecl;
#endif

    public static bool IsWindowsHost =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static bool IsDesktopVk =>
#if STARDIVE_DESKTOPVK
        true;
#else
        false;
#endif
}
