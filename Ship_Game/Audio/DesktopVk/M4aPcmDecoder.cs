#if STARDIVE_DESKTOPVK
using System;
using System.IO;
using SharpJaad.AAC;
using SharpJaad.MP4;
using SharpJaad.MP4.API;

#nullable enable

namespace Ship_Game.Audio.DesktopVk;

/// <summary>
/// Decode AAC-in-MP4 (.m4a/.mp4) and raw .aac to little-endian PCM16 for
/// MonoGame <c>SoundEffect</c>. WindowsDX uses NAudio+MediaFoundation instead;
/// FAudio/SoundEffect.FromStream only accepts WAV.
/// </summary>
static class M4aPcmDecoder
{
    public readonly struct PcmClip
    {
        public readonly byte[] Pcm16;
        public readonly int SampleRate;
        public readonly int Channels;

        public PcmClip(byte[] pcm16, int sampleRate, int channels)
        {
            Pcm16 = pcm16;
            SampleRate = sampleRate;
            Channels = channels;
        }
    }

    public static bool IsSupportedPath(string path)
    {
        return path.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".aac", StringComparison.OrdinalIgnoreCase);
    }

    public static PcmClip DecodeFile(string path)
    {
        if (path.EndsWith(".aac", StringComparison.OrdinalIgnoreCase))
            return DecodeAdtsFile(path);

        using FileStream fs = File.OpenRead(path);
        var container = new MP4Container(fs);
        Movie movie = container.GetMovie()
            ?? throw new InvalidOperationException($"No movie in '{path}'");
        var tracks = movie.GetTracks(AudioTrack.AudioCodec.AAC);
        if (tracks.Count == 0)
            throw new InvalidOperationException($"No AAC audio track in '{path}'");

        var track = (AudioTrack)tracks[0];
        var decoder = new Decoder(track.GetDecoderSpecificInfo());
        var buffer = new SampleBuffer();
        buffer.SetBigEndian(false);

        using var pcm = new MemoryStream(capacity: 64 * 1024);
        int sampleRate = 0;
        int channels = 0;
        while (track.HasMoreFrames())
        {
            Frame frame = track.ReadNextFrame();
            byte[]? data = frame.GetData();
            if (data == null || data.Length == 0)
                continue;
            decoder.DecodeFrame(data, buffer);
            if (buffer.Data is { Length: > 0 })
            {
                sampleRate = buffer.SampleRate;
                channels = buffer.Channels;
                if (buffer.BitsPerSample != 16)
                    throw new InvalidOperationException(
                        $"Unexpected AAC bit depth {buffer.BitsPerSample} in '{path}' (want 16)");
                pcm.Write(buffer.Data, 0, buffer.Data.Length);
            }
        }

        if (pcm.Length == 0 || sampleRate <= 0 || channels <= 0)
            throw new InvalidOperationException($"Decoded no PCM from '{path}'");

        return new PcmClip(pcm.ToArray(), sampleRate, channels);
    }

    static PcmClip DecodeAdtsFile(string path)
    {
        using FileStream fs = File.OpenRead(path);
        var demux = new SharpJaad.ADTS.ADTSDemultiplexer(fs);
        var decoder = new Decoder(demux.GetDecoderSpecificInfo());
        var buffer = new SampleBuffer();
        buffer.SetBigEndian(false);

        using var pcm = new MemoryStream(capacity: 64 * 1024);
        int sampleRate = 0;
        int channels = 0;
        byte[]? frame;
        while ((frame = demux.ReadNextFrame()) != null && frame.Length > 0)
        {
            decoder.DecodeFrame(frame, buffer);
            if (buffer.Data is { Length: > 0 })
            {
                sampleRate = buffer.SampleRate;
                channels = buffer.Channels;
                if (buffer.BitsPerSample != 16)
                    throw new InvalidOperationException(
                        $"Unexpected AAC bit depth {buffer.BitsPerSample} in '{path}' (want 16)");
                pcm.Write(buffer.Data, 0, buffer.Data.Length);
            }
        }

        if (pcm.Length == 0 || sampleRate <= 0 || channels <= 0)
            throw new InvalidOperationException($"Decoded no PCM from '{path}'");

        return new PcmClip(pcm.ToArray(), sampleRate, channels);
    }
}
#endif
