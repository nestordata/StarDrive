#if STARDIVE_DESKTOPVK
using System;

namespace Ship_Game.Audio;

/// <summary>
/// DesktopVK stub — device hot-plug is handled by the OS/FAudio; no WASAPI enumerator.
/// </summary>
public sealed class AudioDevices : IDisposable
{
    public bool ShouldReloadAudioDevice { get; set; }
    public string CurrentDeviceName { get; private set; } = "Default";

    public bool PickAudioDevice(out object selected)
    {
        selected = CurrentDeviceName;
        return true;
    }

    public void SetUserPreference(string deviceId) => GlobalStats.SoundDevice = deviceId ?? "Default";

    public void HandleEvents() { }

    public void Dispose() { }
}
#endif
