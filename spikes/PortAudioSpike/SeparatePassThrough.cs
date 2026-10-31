using PortAudioSharp;

namespace PortAudioSpike;

/// <summary>
/// Pass-through over two streams: the input callback writes into a single-producer,
/// single-consumer ring buffer and the output callback reads from it. Both callbacks only
/// touch fields and preallocated arrays: no allocation, locks or I/O.
/// </summary>
internal sealed class SeparatePassThrough
{
    private const int RingFrames = 8192;
    private const double DriftWindowSeconds = 0.5;

    private readonly float[] ring = new float[RingFrames];
    private readonly bool mute;
    private readonly int driftWindowCallbacks;
    private long written;
    private long read;
    private long inputCallbacks;
    private long outputCallbacks;
    private long inputOverflows;
    private long outputUnderflows;
    private long ringOverruns;
    private long ringUnderruns;
    private long skips;
    private long skippedFrames;
    private long fillSum;
    private long windowMinimum = long.MaxValue;
    private int windowCallbacks;

    public SeparatePassThrough(uint frames, double sampleRate, bool mute)
    {
        this.mute = mute;
        driftWindowCallbacks = Math.Max(1, (int)(sampleRate * DriftWindowSeconds / frames));
    }

    public long InputCallbacks => Interlocked.Read(ref inputCallbacks);

    public long OutputCallbacks => Interlocked.Read(ref outputCallbacks);

    public long InputOverflows => Interlocked.Read(ref inputOverflows);

    public long OutputUnderflows => Interlocked.Read(ref outputUnderflows);

    public long RingOverruns => Interlocked.Read(ref ringOverruns);

    public long RingUnderruns => Interlocked.Read(ref ringUnderruns);

    /// <summary>How often the output dropped frames to correct clock drift.</summary>
    public long Skips => Interlocked.Read(ref skips);

    public long SkippedFrames => Interlocked.Read(ref skippedFrames);

    /// <summary>The average number of frames waiting in the ring when the output reads, before any drift correction.</summary>
    public double AverageFill
    {
        get
        {
            long callbacks = OutputCallbacks;
            return callbacks == 0 ? 0 : (double)Interlocked.Read(ref fillSum) / callbacks;
        }
    }

    public unsafe StreamCallbackResult InputCallback(
        IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        Interlocked.Increment(ref inputCallbacks);
        if ((statusFlags & StreamCallbackFlags.InputOverflow) != 0)
        {
            Interlocked.Increment(ref inputOverflows);
        }

        long writeAt = written;
        if (writeAt + frameCount - Volatile.Read(ref read) > RingFrames)
        {
            Interlocked.Increment(ref ringOverruns);
            return StreamCallbackResult.Continue;
        }

        ReadOnlySpan<float> samples = new((void*)input, (int)frameCount);
        for (int frame = 0; frame < samples.Length; frame++)
        {
            ring[(int)((writeAt + frame) % RingFrames)] = samples[frame];
        }

        Volatile.Write(ref written, writeAt + frameCount);
        return StreamCallbackResult.Continue;
    }

    public unsafe StreamCallbackResult OutputCallback(
        IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        Interlocked.Increment(ref outputCallbacks);
        if ((statusFlags & StreamCallbackFlags.OutputUnderflow) != 0)
        {
            Interlocked.Increment(ref outputUnderflows);
        }

        long writtenUpTo = Volatile.Read(ref written);
        long available = writtenUpTo - read;
        Interlocked.Add(ref fillSum, available);
        long readAt = read + DriftCorrection(available, frameCount);

        Span<float> stereo = new((void*)output, (int)frameCount * Program.OutputChannels);
        if (writtenUpTo - readAt < frameCount)
        {
            Interlocked.Increment(ref ringUnderruns);
            stereo.Clear();
            Volatile.Write(ref read, readAt);
            return StreamCallbackResult.Continue;
        }

        for (int frame = 0; frame < frameCount; frame++)
        {
            float sample = mute ? 0 : ring[(int)((readAt + frame) % RingFrames)];
            stereo[2 * frame] = sample;
            stereo[(2 * frame) + 1] = sample;
        }

        Volatile.Write(ref read, readAt + frameCount);
        return StreamCallbackResult.Continue;
    }

    // The input arrives in bursts, so one full reading says nothing; only a backlog that never
    // drops below one buffer for a whole window is drift (or the start offset) and is dropped.
    private long DriftCorrection(long available, uint frameCount)
    {
        windowMinimum = Math.Min(windowMinimum, available);
        if (++windowCallbacks < driftWindowCallbacks)
        {
            return 0;
        }

        long excess = windowMinimum - frameCount;
        windowMinimum = long.MaxValue;
        windowCallbacks = 0;
        if (excess <= 0)
        {
            return 0;
        }

        Interlocked.Increment(ref skips);
        Interlocked.Add(ref skippedFrames, excess);
        return excess;
    }
}
