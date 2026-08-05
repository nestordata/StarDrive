using System;
using Microsoft.Xna.Framework;

namespace Ship_Game.Platform;

public interface IDisplayInfo
{
    Rectangle PrimaryBounds { get; }
}

public interface IClipboard
{
    void SetText(string text);
}

public interface INativeDialogs
{
    void ShowError(string title, string message);
    void ShowInfo(string title, string message);
}

public interface IAppProcess
{
    string ExecutablePath { get; }
    void RequestExit();
}

public interface IKeyboardExtras
{
    bool IsCapsLockDown { get; }
}

public interface ISingleInstanceLock : IDisposable
{
    bool UniqueInstance { get; }
}

public interface IWindowChrome
{
    /// <summary>Apply windowed / borderless / fullscreen chrome for the given mode.</summary>
    void ApplyWindowMode(GameWindow window, global::Ship_Game.WindowMode mode, int width, int height);

    /// <summary>Center a non-fullscreen window on the primary display.</summary>
    void CenterWindow(GameWindow window, int width, int height);
}

public static class PlatformServices
{
    public static IDisplayInfo Display { get; private set; }
    public static IClipboard Clipboard { get; private set; }
    public static INativeDialogs Dialogs { get; private set; }
    public static IAppProcess App { get; private set; }
    public static IKeyboardExtras Keyboard { get; private set; }
    public static IWindowChrome WindowChrome { get; private set; }

    public static void Initialize()
    {
#if STARDIVE_WINDOWSDX
        Display = new Windows.WindowsDisplayInfo();
        Clipboard = new Windows.WindowsClipboard();
        Dialogs = new Windows.WindowsNativeDialogs();
        App = new Windows.WindowsAppProcess();
        Keyboard = new Windows.WindowsKeyboardExtras();
        WindowChrome = new Windows.WindowsWindowChrome();
#else
        Display = new DesktopVk.DesktopVkDisplayInfo();
        Clipboard = new DesktopVk.DesktopVkClipboard();
        Dialogs = new DesktopVk.DesktopVkNativeDialogs();
        App = new DesktopVk.DesktopVkAppProcess();
        Keyboard = new DesktopVk.DesktopVkKeyboardExtras();
        WindowChrome = new DesktopVk.DesktopVkWindowChrome();
#endif
    }

    public static ISingleInstanceLock CreateSingleInstanceLock()
    {
#if STARDIVE_WINDOWSDX
        return new Windows.WindowsSingleInstanceLock();
#else
        return new DesktopVk.DesktopVkSingleInstanceLock();
#endif
    }
}
