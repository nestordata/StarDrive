#if STARDIVE_DESKTOPVK
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SDUtils;

namespace Ship_Game.Platform.DesktopVk;

public sealed class DesktopVkDisplayInfo : IDisplayInfo
{
    public Rectangle PrimaryBounds
    {
        get
        {
            // GraphicsAdapter is only safe after Game.Run has created the SDL window.
            // Querying it earlier pre-inits Vulkan and crashes in SDL_Vulkan_GetInstanceExtensions.
            try
            {
                if (global::Ship_Game.GameBase.Base?.GraphicsDevice != null)
                {
                    var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                    return new Rectangle(0, 0, mode.Width, mode.Height);
                }
            }
            catch { /* fall through */ }

            return NativePrimaryBoundsOrDefault();
        }
    }

    static Rectangle NativePrimaryBoundsOrDefault()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                // CoreGraphics — no MonoGame/SDL dependency.
                IntPtr display = CGMainDisplayID();
                CGRect bounds = CGDisplayBounds(display);
                int w = Math.Max(800, (int)bounds.size.width);
                int h = Math.Max(600, (int)bounds.size.height);
                return new Rectangle(0, 0, w, h);
            }
            catch { /* fall through */ }
        }
        return new Rectangle(0, 0, 1920, 1080);
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    static extern IntPtr CGMainDisplayID();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    static extern CGRect CGDisplayBounds(IntPtr display);

    [StructLayout(LayoutKind.Sequential)]
    struct CGSize { public double width; public double height; }

    [StructLayout(LayoutKind.Sequential)]
    struct CGPoint { public double x; public double y; }

    [StructLayout(LayoutKind.Sequential)]
    struct CGRect { public CGPoint origin; public CGSize size; }
}

public sealed class DesktopVkClipboard : IClipboard
{
    public void SetText(string text)
    {
        try
        {
            // DesktopVK product host is macOS Apple Silicon — pbcopy only.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return;
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "pbcopy",
                RedirectStandardInput = true,
                UseShellExecute = false
            });
            p?.StandardInput.Write(text);
            p?.StandardInput.Close();
            p?.WaitForExit(1000);
        }
        catch { /* ignore */ }
    }
}

public sealed class DesktopVkNativeDialogs : INativeDialogs
{
    public void ShowError(string title, string message)
    {
        Console.Error.WriteLine($"[{title}] {message}");
        TryOsDialog(title, message);
    }

    public void ShowInfo(string title, string message)
    {
        Console.WriteLine($"[{title}] {message}");
        TryOsDialog(title, message);
    }

    static void TryOsDialog(string title, string message)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string script = $"display dialog \"{EscapeAppleScript(message)}\" with title \"{EscapeAppleScript(title)}\" buttons {{\"OK\"}} default button \"OK\"";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "osascript",
                    ArgumentList = { "-e", script },
                    UseShellExecute = false
                })?.WaitForExit(15000);
            }
        }
        catch { /* console already logged */ }
    }

    static string EscapeAppleScript(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
}

public sealed class DesktopVkAppProcess : IAppProcess
{
    public string ExecutablePath => Environment.ProcessPath
                                    ?? Process.GetCurrentProcess().MainModule?.FileName
                                    ?? Assembly.GetExecutingAssembly().Location;

    public void RequestExit() => Environment.Exit(0);
}

public sealed class DesktopVkKeyboardExtras : IKeyboardExtras
{
    // MonoGame does not expose CapsLock state portably; treat as unlocked.
    public bool IsCapsLockDown => false;
}

public sealed class DesktopVkSingleInstanceLock : ISingleInstanceLock
{
    readonly string LockPath;
    FileStream Stream;
    public bool UniqueInstance { get; }

    public DesktopVkSingleInstanceLock()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".stardrive");
        Directory.CreateDirectory(dir);
        LockPath = Path.Combine(dir, "stardrive.lock");
        try
        {
            Stream = new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            UniqueInstance = true;
        }
        catch (IOException)
        {
            UniqueInstance = false;
        }
    }

    public void Dispose()
    {
        Stream?.Dispose();
        Stream = null;
        try { if (UniqueInstance && File.Exists(LockPath)) File.Delete(LockPath); } catch { }
        GC.SuppressFinalize(this);
    }
}

public sealed class DesktopVkWindowChrome : IWindowChrome
{
    public void ApplyWindowMode(GameWindow window, global::Ship_Game.WindowMode mode, int width, int height)
    {
        // DesktopVK/SDL: borderless vs windowed is primarily handled via
        // GraphicsDeviceManager.IsFullScreen / ToggleFullScreen in GameBase.
        window.AllowUserResizing = mode == global::Ship_Game.WindowMode.Windowed;
        try { window.Title = "StarDrive"; } catch { /* ignore */ }
    }

    public void CenterWindow(GameWindow window, int width, int height)
    {
        // SDL backends typically center on first show; Position may be unsupported.
        try
        {
            var bounds = PlatformServices.Display.PrimaryBounds;
            window.Position = new Point(
                Math.Max(0, bounds.X + bounds.Width / 2 - width / 2),
                Math.Max(0, bounds.Y + bounds.Height / 2 - height / 2));
        }
        catch
        {
            // Some MonoGame native backends ignore Position — non-fatal.
        }
    }
}
#endif
