using System.Numerics;

namespace SolidBackup.Audio;

/// <summary>
/// Lock-free hand-over from the input stream's thread (the only writer) to the output stream's
/// thread (the only reader). Neither side allocates, locks or waits.
/// </summary>
public sealed class HandOverBuffer
{
    // E2-1: the input arrives in bursts and the two device clocks drift, so only a backlog that
    // stays above one buffer for this long is dropped; dropping on every burst starves the output.
    private const double DriftWindowSeconds = 0.5;

    private readonly float[] _ring;
    private readonly int _mask;
    private readonly int _driftWindowReads;
    private long _written;
    private long _read;
    private long _overruns;
    private long _underruns;
    private long _driftCorrections;
    private long _windowMinimum = long.MaxValue;
    private int _windowReads;
    private bool _primed;

    public HandOverBuffer(int sampleRate, int bufferSize)
    {
        // Room for half a second, so a stalled output loses input only after a long stall.
        _ring = new float[BitOperations.RoundUpToPowerOf2((uint)Math.Max(sampleRate / 2, bufferSize * 8))];
        _mask = _ring.Length - 1;
        _driftWindowReads = Math.Max(1, (int)(sampleRate * DriftWindowSeconds / bufferSize));
    }

    /// <summary>Input buffers dropped because the ring was full.</summary>
    public long Overruns => Interlocked.Read(ref _overruns);

    /// <summary>
    /// Output buffers played as silence because less than one buffer was waiting, counted only
    /// once the hand-over is primed: the silence before the input first delivers is not a dropout.
    /// </summary>
    public long Underruns => Interlocked.Read(ref _underruns);

    /// <summary>Times a persistent backlog was dropped to keep the delay at one buffer.</summary>
    public long DriftCorrections => Interlocked.Read(ref _driftCorrections);

    /// <summary>Empties the hand-over for a new run; call it only while neither side is running.</summary>
    public void Reset()
    {
        _written = 0;
        _read = 0;
        _windowMinimum = long.MaxValue;
        _windowReads = 0;
        _primed = false;
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        long writeAt = _written;
        if (writeAt + samples.Length - Volatile.Read(ref _read) > _ring.Length)
        {
            Interlocked.Increment(ref _overruns);
            return;
        }

        int start = (int)(writeAt & _mask);
        int first = Math.Min(samples.Length, _ring.Length - start);
        samples[..first].CopyTo(_ring.AsSpan(start));
        samples[first..].CopyTo(_ring);
        Volatile.Write(ref _written, writeAt + samples.Length);
    }

    public void Read(Span<float> destination)
    {
        long writtenUpTo = Volatile.Read(ref _written);
        long readAt = _read;
        readAt += DriftExcess(writtenUpTo - readAt, destination.Length);
        if (writtenUpTo - readAt < destination.Length)
        {
            if (_primed)
            {
                Interlocked.Increment(ref _underruns);
            }

            destination.Clear();
            Volatile.Write(ref _read, readAt);
            return;
        }

        _primed = true;
        int start = (int)(readAt & _mask);
        int first = Math.Min(destination.Length, _ring.Length - start);
        _ring.AsSpan(start, first).CopyTo(destination);
        _ring.AsSpan(0, destination.Length - first).CopyTo(destination[first..]);
        Volatile.Write(ref _read, readAt + destination.Length);
    }

    private long DriftExcess(long available, int frames)
    {
        _windowMinimum = Math.Min(_windowMinimum, available);
        if (++_windowReads < _driftWindowReads)
        {
            return 0;
        }

        long excess = _windowMinimum - frames;
        _windowMinimum = long.MaxValue;
        _windowReads = 0;
        if (excess <= 0)
        {
            return 0;
        }

        Interlocked.Increment(ref _driftCorrections);
        return excess;
    }
}
