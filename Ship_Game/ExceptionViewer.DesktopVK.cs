#if STARDIVE_DESKTOPVK
using Ship_Game.Platform;

namespace Ship_Game;

/// <summary>
/// DesktopVK replacement for the WinForms ExceptionViewer dialog.
/// Does not open a browser or auto-file GitHub issues.
/// </summary>
public static class ExceptionViewer
{
    public static void ShowExceptionDialog(string dialogText, bool autoReport)
    {
        string description = autoReport
            ? "This error was submitted automatically to our exception tracking system."
            : "Automatic error reporting is disabled.";

        PlatformServices.Clipboard.SetText(dialogText ?? "");
        string message = description + "\n\n" + (dialogText ?? "");
        if (message.Length > 1500)
            message = message.Substring(0, 1500) + "\n... (truncated; full text copied to clipboard when possible)";

        PlatformServices.Dialogs.ShowError("StarDrive Error", message);
    }
}
#endif
