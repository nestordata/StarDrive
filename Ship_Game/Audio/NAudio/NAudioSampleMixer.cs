#if STARDIVE_WINDOWSDX
﻿using System;
using NAudio.Utils;
using NAudio.Wave;
using SDUtils;

namespace Ship_Game.Audio.NAudio;

/// <summary>
/// A sample provider mixer, allowing inputs to be added and removed
/// </summary>
internal class NAudioSampleMixer : ISampleProvider, IDisposable
{
    readonly Array<NAudioSampleInstance> Sources = new();
    readonly Array<NAudioSampleInstance> Processing = new();
    readonly Array<NAudioSampleInstance> ToRemove = new();
    float[] SrcBuffer;
    const int MaxInputs = 1024; // protect ourselves against doing something silly
    
    /// <summary>
    /// The output WaveFormat of this sample provider
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Creates a new MixingSampleProvider, with no inputs, but a specified WaveFormat
    /// </summary>
    /// <param name="waveFormat">The WaveFormat of this mixer. All inputs must be in this format</param>
    public NAudioSampleMixer(WaveFormat waveFormat)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            throw new ArgumentException("Mixer wave format must be IEEE float");
        WaveFormat = waveFormat;
    }
    
    /// <summary>
    /// When set to true, the Read method always returns the number
    /// of samples requested, even if there are no inputs, or if the
    /// current inputs reach their end. Setting this to true effectively
    /// makes this a never-ending sample provider, so take care if you plan
    /// to write it out to a file.
    /// </summary>
    public bool ReadFully { get; set; }

    /// <summary>
    /// Master volume multiplier applied to the mixed output. 1.0 = pass-through, 0.0 = silent.
    /// Used to mute NAudio output without affecting per-instance volumes or stopping samples
    /// (which would trigger MainMenu music restart loops). Process-wide audio sessions
    /// (MediaFoundation video, etc.) are unaffected since they don't go through this mixer.
    /// `volatile` so the WASAPI render thread sees writes from the main thread promptly.
    /// </summary>
    public volatile float MasterVolume = 1.0f;

    public void Dispose()
    {
        lock (Sources)
        {
            foreach (NAudioSampleInstance source in Sources)
                source.Dispose();
            Sources.Clear();
        }
    }

    /// <summary>
    /// Adds a new mixer input
    /// </summary>
    public void AddMixerInput(NAudioSampleInstance instance)
    {
        // check that the input is in the right format
        // it's better to crash here than in the main mixer loop
        WaveFormat inFormat = instance.WaveFormat;
        if (WaveFormat.SampleRate != inFormat.SampleRate || WaveFormat.Channels != inFormat.Channels)
            throw new ArgumentException("All mixer inputs must have the same WaveFormat");

        // we'll just call the lock around add since we are protecting against an AddMixerInput at
        // the same time as a Read, rather than two AddMixerInput calls at the same time
        lock (Sources)
        {
            if (Sources.Count >= MaxInputs)
                throw new InvalidOperationException("Too many mixer inputs");
            Sources.Add(instance);
        }
    }

    /// <summary>
    /// Reads samples from this sample provider
    /// </summary>
    /// <param name="buffer">Sample buffer</param>
    /// <param name="offset">Offset into sample buffer</param>
    /// <param name="count">Number of samples required</param>
    /// <returns>Number of samples read</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        int outputSamples = 0;
        SrcBuffer = BufferHelpers.Ensure(SrcBuffer, count);

        // grab a copy of sources to process, since we don't want to hold a lock while processing
        lock (Sources)
            Processing.Assign(Sources);

        foreach (NAudioSampleInstance source in Processing.AsReadOnlySpan())
        {
            int samplesRead = source.Read(SrcBuffer, 0, count);
            int outIndex = offset;
            for (int i = 0; i < samplesRead; i++)
            {
                // first source in this index
                if (i >= outputSamples)
                    buffer[outIndex++] = SrcBuffer[i];
                else // additive mix with previous sources
                    buffer[outIndex++] += SrcBuffer[i];
            }

            outputSamples = Math.Max(samplesRead, outputSamples);
            if (source.IsDisposed || source.CanBeDisposed)
                ToRemove.Add(source);
        }

        Processing.Clear();
        if (ToRemove.NotEmpty)
        {
            lock (Sources)
                foreach (NAudioSampleInstance source in ToRemove)
                    Sources.Remove(source);
            ToRemove.Clear();
        }

        // optionally ensure we return a full buffer
        if (ReadFully && outputSamples < count)
        {
            int outputIndex = offset + outputSamples;
            while (outputIndex < offset + count)
                buffer[outputIndex++] = 0;
            outputSamples = count;
        }

        // apply master volume to the mixed output (used for video-playback NAudio mute).
        // Snapshot once — main thread may flip MasterVolume between the gate and the loop.
        float vol = MasterVolume;
        if (vol != 1.0f)
        {
            int end = offset + outputSamples;
            for (int i = offset; i < end; i++)
                buffer[i] *= vol;
        }

        return outputSamples;
    }
}

#endif
