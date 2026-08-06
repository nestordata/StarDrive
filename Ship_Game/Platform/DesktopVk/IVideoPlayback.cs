#if STARDIVE_DESKTOPVK
using System;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace Ship_Game.Platform.DesktopVk;

/// <summary>
/// Platform video backend used by ScreenMediaPlayer on DesktopVK (FFmpeg/SDVideo).
/// WindowsDX keeps MonoGame Media Foundation VideoPlayer directly.
/// </summary>
public interface IVideoPlayback : IDisposable
{
    bool IsDisposed { get; }
    bool IsOpen { get; }
    MediaState State { get; }
    int Width { get; }
    int Height { get; }
    TimeSpan PlayPosition { get; }
    float Volume { get; set; }
    bool IsLooped { get; set; }

    /// <summary>Open file and start decoding. Path is absolute or Content-relative.</summary>
    bool Open(string filePath);

    void Play();
    void Pause();
    void Resume();
    void Stop();

    /// <summary>Upload latest RGBA frame into a managed Texture2D (created/resized as needed).</summary>
    Texture2D GetTexture(GraphicsDevice device);

    /// <summary>Drain PCM from the decoder into FAudio (call from Update/Draw).</summary>
    void PumpAudio();
}
#endif
