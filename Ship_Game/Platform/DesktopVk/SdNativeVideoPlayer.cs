#if STARDIVE_DESKTOPVK
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using SDUtils;

namespace Ship_Game.Platform.DesktopVk;

/// <summary>
/// FFmpeg-backed video player via libSDNative SDVideo API.
/// </summary>
public sealed class SdNativeVideoPlayer : IVideoPlayback
{
    IntPtr Handle;
    Texture2D Texture;
    byte[] UploadScratch;
    DynamicSoundEffectInstance Audio;
    readonly byte[] AudioScratch = new byte[8192];
    float _volume = 1f;
    bool _looped;

    public bool IsDisposed { get; private set; }
    public bool IsOpen => Handle != IntPtr.Zero;

    public MediaState State
    {
        get
        {
            if (Handle == IntPtr.Zero) return MediaState.Stopped;
            return Native.SDVideoGetState(Handle) switch
            {
                1 => MediaState.Playing,
                2 => MediaState.Paused,
                _ => MediaState.Stopped,
            };
        }
    }

    public int Width => Handle == IntPtr.Zero ? 0 : Native.SDVideoGetWidth(Handle);
    public int Height => Handle == IntPtr.Zero ? 0 : Native.SDVideoGetHeight(Handle);

    public TimeSpan PlayPosition => Handle == IntPtr.Zero
        ? TimeSpan.Zero
        : TimeSpan.FromSeconds(Native.SDVideoGetPositionSeconds(Handle));

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (Handle != IntPtr.Zero)
                Native.SDVideoSetVolume(Handle, _volume);
            if (Audio != null && !Audio.IsDisposed)
                Audio.Volume = _volume;
        }
    }

    public bool IsLooped
    {
        get => _looped;
        set
        {
            _looped = value;
            if (Handle != IntPtr.Zero)
                Native.SDVideoSetLooped(Handle, value ? 1 : 0);
        }
    }

    public static bool IsSupported()
    {
        try { return Native.SDVideoIsSupported() != 0; }
        catch { return false; }
    }

    public bool Open(string filePath)
    {
        if (IsDisposed)
            return false;
        CloseNative();
        if (IsDisposed || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        Handle = Native.SDVideoOpen(filePath);
        if (Handle == IntPtr.Zero)
            return false;
        if (IsDisposed)
        {
            CloseNative();
            return false;
        }

        Native.SDVideoSetVolume(Handle, _volume);
        Native.SDVideoSetLooped(Handle, _looped ? 1 : 0);
        return true;
    }

    public void Play()
    {
        if (Handle != IntPtr.Zero)
            Native.SDVideoPlay(Handle);
        EnsureAudioPlaying();
    }

    public void Pause()
    {
        if (Handle != IntPtr.Zero)
            Native.SDVideoPause(Handle);
        if (Audio is { IsDisposed: false, State: SoundState.Playing })
            Audio.Pause();
    }

    public void Resume()
    {
        if (Handle != IntPtr.Zero)
            Native.SDVideoPlay(Handle);
        EnsureAudioPlaying();
    }

    public void Stop()
    {
        if (Handle != IntPtr.Zero)
            Native.SDVideoStop(Handle);
        if (Audio is { IsDisposed: false })
            Audio.Stop();
    }

    void EnsureAudioPlaying()
    {
        if (Audio is { IsDisposed: false, State: not SoundState.Playing })
            Audio.Play();
    }

    public void PumpAudio()
    {
        if (Handle == IntPtr.Zero || State != MediaState.Playing)
            return;

        int sampleRate = 0, channels = 0;
        int n = Native.SDVideoReadAudio(Handle, AudioScratch, AudioScratch.Length, out sampleRate, out channels);
        if (n <= 0 || sampleRate <= 0 || channels <= 0)
            return;

        try
        {
            if (Audio == null || Audio.IsDisposed)
            {
                var ch = channels >= 2 ? AudioChannels.Stereo : AudioChannels.Mono;
                Audio = new DynamicSoundEffectInstance(sampleRate, ch) { Volume = _volume };
                Audio.Play();
            }
            // Keep a small buffer queue filled.
            while (Audio.PendingBufferCount < 4)
            {
                if (n > 0)
                    Audio.SubmitBuffer(AudioScratch, 0, n);
                n = Native.SDVideoReadAudio(Handle, AudioScratch, AudioScratch.Length, out _, out _);
                if (n <= 0) break;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"SdNativeVideoPlayer.PumpAudio: {ex.Message}");
        }
    }

    public unsafe Texture2D GetTexture(GraphicsDevice device)
    {
        if (Handle == IntPtr.Zero || device == null || device.IsDisposed)
            return Texture;

        byte* rgba = null;
        int w = 0, h = 0, stride = 0;
        if (Native.SDVideoLockFrame(Handle, out rgba, out w, out h, out stride) == 0 || rgba == null)
            return Texture;

        try
        {
            if (Texture == null || Texture.IsDisposed || Texture.Width != w || Texture.Height != h)
            {
                Texture?.Dispose();
                Texture = new Texture2D(device, w, h, false, SurfaceFormat.Color);
                UploadScratch = new byte[w * h * 4];
            }

            // Copy tightly packed RGBA rows (stride may include padding).
            int dstStride = w * 4;
            if (stride == dstStride)
            {
                Marshal.Copy((IntPtr)rgba, UploadScratch, 0, dstStride * h);
            }
            else
            {
                for (int y = 0; y < h; ++y)
                    Marshal.Copy((IntPtr)(rgba + y * stride), UploadScratch, y * dstStride, dstStride);
            }
            Texture.SetData(UploadScratch);
            return Texture;
        }
        finally
        {
            Native.SDVideoUnlockFrame(Handle);
        }
    }

    void CloseNative()
    {
        if (Handle != IntPtr.Zero)
        {
            Native.SDVideoClose(Handle);
            Handle = IntPtr.Zero;
        }
        if (Audio != null)
        {
            try { Audio.Stop(); Audio.Dispose(); } catch { /* ignore */ }
            Audio = null;
        }
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        CloseNative();
        Texture?.Dispose();
        Texture = null;
        GC.SuppressFinalize(this);
    }

    ~SdNativeVideoPlayer() { Dispose(); }

    static class Native
    {
        const string Lib = NativeLib.Name;
        const CallingConvention CC = NativeLib.CallConv;

        [DllImport(Lib, CallingConvention = CC)]
        public static extern int SDVideoIsSupported();

        [DllImport(Lib, CallingConvention = CC)]
        public static extern IntPtr SDVideoOpen([MarshalAs(UnmanagedType.LPWStr)] string fileName);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern void SDVideoClose(IntPtr video);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern void SDVideoPlay(IntPtr video);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern void SDVideoPause(IntPtr video);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern void SDVideoStop(IntPtr video);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern int SDVideoGetState(IntPtr video);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern int SDVideoGetWidth(IntPtr video);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern int SDVideoGetHeight(IntPtr video);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern double SDVideoGetPositionSeconds(IntPtr video);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern void SDVideoSetVolume(IntPtr video, float volume01);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern void SDVideoSetLooped(IntPtr video, int looped);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern unsafe int SDVideoLockFrame(IntPtr video, out byte* outRgba, out int outWidth, out int outHeight, out int outStride);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern void SDVideoUnlockFrame(IntPtr video);

        [DllImport(Lib, CallingConvention = CC)]
        public static extern int SDVideoReadAudio(IntPtr video, byte[] outPcm, int maxBytes, out int outSampleRate, out int outChannels);
    }
}
#endif
