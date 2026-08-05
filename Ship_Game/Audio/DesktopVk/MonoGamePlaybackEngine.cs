#if STARDIVE_DESKTOPVK
using System;
using System.IO;
using Microsoft.Xna.Framework.Audio;
using SDUtils;
using MgSoundEffect = Microsoft.Xna.Framework.Audio.SoundEffect;
using MgSoundEffectInstance = Microsoft.Xna.Framework.Audio.SoundEffectInstance;

#nullable enable

namespace Ship_Game.Audio.DesktopVk;

/// <summary>
/// DesktopVK audio backend using MonoGame/FAudio SoundEffect.
/// </summary>
internal sealed class MonoGamePlaybackEngine : IDisposable
{
    readonly object CacheLock = new();
    readonly Map<string, MgSoundEffect> Cache = new();
    float _mixerMaster = 1f;
    float _deviceVolume = 1f;

    public float Volume
    {
        get => _deviceVolume;
        set => _deviceVolume = Math.Clamp(value, 0f, 1f);
    }

    public float MixerMasterVolume
    {
        get => _mixerMaster;
        set => _mixerMaster = float.IsNaN(value) ? 1f : Math.Clamp(value, 0f, 1f);
    }

    public IAudioInstance? Play(AudioCategory category, AudioEmitter? emitter, string audioFile, float volume)
    {
        try
        {
            float? effective = emitter?.GetEffectiveVolume(category, volume);
            if (effective < 0.0001f)
                return null;

            MgSoundEffect? effect = GetOrLoad(audioFile);
            if (effect == null)
                return null;

            float gain = (effective ?? volume) * _mixerMaster * _deviceVolume;
            MgSoundEffectInstance inst = effect.CreateInstance();
            inst.Volume = Math.Clamp(gain, 0f, 1f);
            bool loop = category.Name.IndexOf("Music", StringComparison.OrdinalIgnoreCase) >= 0;
            inst.IsLooped = loop;
            inst.Play();
            return new MonoGameAudioInstance(inst, category);
        }
        catch (Exception ex)
        {
            Log.Warning($"MonoGamePlaybackEngine.Play failed ({audioFile}): {ex.Message}");
            return null;
        }
    }

    MgSoundEffect? GetOrLoad(string path)
    {
        lock (CacheLock)
        {
            if (Cache.TryGetValue(path, out MgSoundEffect? cached) && cached is { IsDisposed: false })
                return cached;
        }

        if (!File.Exists(path))
            return null;

        using var fs = File.OpenRead(path);
        var fx = MgSoundEffect.FromStream(fs);
        lock (CacheLock)
        {
            if (Cache.TryGetValue(path, out MgSoundEffect? raced) && raced is { IsDisposed: false })
            {
                fx.Dispose();
                return raced;
            }
            Cache[path] = fx;
            return fx;
        }
    }

    public void Dispose()
    {
        lock (CacheLock)
        {
            foreach (MgSoundEffect fx in Cache.Values)
                fx?.Dispose();
            Cache.Clear();
        }
    }
}

sealed class MonoGameAudioInstance : IAudioInstance
{
    MgSoundEffectInstance? Inst;
    readonly AudioCategory Category;

    public MonoGameAudioInstance(MgSoundEffectInstance inst, AudioCategory category)
    {
        Inst = inst;
        Category = category;
    }

    public bool IsPlaying => Inst is { State: SoundState.Playing };
    public bool IsPaused => Inst is { State: SoundState.Paused };
    public bool IsStopped => Inst == null || Inst.State == SoundState.Stopped || IsDisposed;
    public bool IsDisposed { get; private set; }
    public bool CanBeDisposed => IsStopped;
    public float Volume => Inst?.Volume ?? 0f;

    public void Pause() => Inst?.Pause();
    public void Resume() => Inst?.Resume();

    public void Stop(bool fadeout)
    {
        _ = fadeout;
        _ = Category;
        Inst?.Stop();
        Dispose();
    }

    public void SetVolume(float volume)
    {
        if (Inst != null)
            Inst.Volume = Math.Clamp(volume, 0f, 1f);
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        Inst?.Dispose();
        Inst = null;
    }
}
#endif
