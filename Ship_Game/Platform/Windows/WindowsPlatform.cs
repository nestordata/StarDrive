#if STARDIVE_WINDOWSDX
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using SDUtils;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace Ship_Game.Platform.Windows;

public sealed class WindowsDisplayInfo : IDisplayInfo
{
    public XnaRectangle PrimaryBounds
    {
        get
        {
            var b = Screen.PrimaryScreen.Bounds;
            return new XnaRectangle(b.X, b.Y, b.Width, b.Height);
        }
    }
}

public sealed class WindowsClipboard : IClipboard
{
    public void SetText(string text)
    {
        try { Clipboard.SetText(text); }
        catch { /* ignore clipboard races */ }
    }
}

public sealed class WindowsNativeDialogs : INativeDialogs
{
    public void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public void ShowInfo(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
}

public sealed class WindowsAppProcess : IAppProcess
{
    public string ExecutablePath => Application.ExecutablePath;
    public void RequestExit() => Application.Exit();
}

public sealed class WindowsKeyboardExtras : IKeyboardExtras
{
    public bool IsCapsLockDown => Control.IsKeyLocked(Keys.Capital);
}

public sealed class WindowsSingleInstanceLock : ISingleInstanceLock
{
    Mutex Mutex;
    public bool UniqueInstance { get; }

    public WindowsSingleInstanceLock()
    {
        try
        {
            string appGuid = ((GuidAttribute)Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(GuidAttribute), false)[0]).Value;
            Mutex = new Mutex(true, $"Global\\{{{appGuid}}}", out bool created);
            UniqueInstance = created;
        }
        catch (AbandonedMutexException)
        {
            UniqueInstance = false;
        }
    }

    public void Dispose()
    {
        Mem.Dispose(ref Mutex);
        GC.SuppressFinalize(this);
    }

    ~WindowsSingleInstanceLock() => Mem.Dispose(ref Mutex);
}

public sealed class WindowsWindowChrome : IWindowChrome
{
    public void ApplyWindowMode(GameWindow window, global::Ship_Game.WindowMode mode, int width, int height)
    {
        var form = (Form)Control.FromHandle(window.Handle);
        form.FormBorderStyle = mode switch
        {
            global::Ship_Game.WindowMode.Windowed => FormBorderStyle.Fixed3D,
            _ => FormBorderStyle.None
        };
    }

    public void CenterWindow(GameWindow window, int width, int height)
    {
        var form = (Form)Control.FromHandle(window.Handle);
        form.WindowState = FormWindowState.Normal;
        form.ClientSize = new DrawingSize(width, height);

        var bounds = Screen.PrimaryScreen.Bounds;
        DrawingSize size = bounds.Size;
        var pt = new DrawingPoint(size.Width / 2 - width / 2, size.Height / 2 - height / 2);
        if (pt.X < bounds.Left) pt.X = bounds.Left;
        if (pt.Y < bounds.Top) pt.Y = bounds.Top;
        form.Location = pt;
    }

    public static Form GetForm(GameWindow window) => (Form)Control.FromHandle(window.Handle);
}
#endif
