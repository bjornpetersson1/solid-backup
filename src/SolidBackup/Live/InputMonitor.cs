using System.Numerics;

using SolidBackup.Configuration;
using SolidBackup.Synthesis;

namespace SolidBackup.Live;

/// <summary>
/// Monitoring (E7-3): the microphone in the headphones with the chords. The input's thread writes each buffer into a
/// ring allocated up front, and the output's thread adds it to the chords at <c>MonitorLevelPercent</c>, kept within
/// full scale by the synthesizer's soft limit. One producer, one consumer, no locks: each side owns one counter. If the
/// input runs ahead (clock drift, or a stalled output), the oldest samples are skipped so at most one buffer waits
/// beyond the one being played; if it runs behind, silence is played for the missing samples. Neither side allocates.
/// </summary>
public sealed class InputMonitor
{
    // Room for several buffers, so a late output thread does not make the input drop samples.
    private const int RingBuffers = 8;

    private readonly float[] _ring;
    private readonly int _mask;
    private readonly int _backlog;
    private readonly float _gain;
    private long _written;
    private long _read;

    public InputMonitor(int bufferSize, int levelPercent)
    {
        _ring = new float[(int)BitOperations.RoundUpToPowerOf2((uint)(bufferSize * RingBuffers))];
        _mask = _ring.Length - 1;
        _backlog = bufferSize;
        _gain = levelPercent / 100f;
    }

    /// <summary>
    /// Whether live mode monitors: with <c>MonitorOn</c>, and only to an output known to be safe (not the built-in
    /// speakers, where the microphone would hear itself).
    /// </summary>
    public static bool Monitors(AppSettings settings, bool outputSafe)
    {
        return settings.MonitorOn && outputSafe;
    }

    /// <summary>A monitor for the settings, or null when live mode does not monitor (<see cref="Monitors"/>).</summary>
    public static InputMonitor? FromSettings(AppSettings settings, bool outputSafe)
    {
        return Monitors(settings, outputSafe) ? new InputMonitor(settings.BufferSize, settings.MonitorLevelPercent) : null;
    }

    /// <summary>
    /// On the input's thread: the next input buffer. When the output has stalled and the ring is full, the oldest
    /// samples are overwritten, so the output resumes with the latest input.
    /// </summary>
    public void Write(ReadOnlySpan<float> samples)
    {
        long written = _written;
        for (int i = 0; i < samples.Length; i++)
        {
            _ring[(written + i) & _mask] = samples[i];
        }

        Volatile.Write(ref _written, written + samples.Length);
    }

    /// <summary>
    /// On the output's thread: adds the waiting input to <paramref name="output"/>, which holds the chords; with
    /// <paramref name="fadeOut"/> the input fades from its level to silence over the buffer, so stopping does not click.
    /// </summary>
    public void MixInto(Span<float> output, bool fadeOut = false)
    {
        long read = _read;
        long available = Volatile.Read(ref _written) - read;
        long excess = available - output.Length - _backlog;
        if (excess > 0)
        {
            read += excess;
            available -= excess;
        }

        // Past the waiting input the chords stay as they are; the synthesizer has already limited them.
        int count = (int)Math.Min(available, output.Length);
        for (int i = 0; i < count; i++)
        {
            float gain = fadeOut ? _gain * (1 - ((float)i / output.Length)) : _gain;
            output[i] = (float)Synthesizer.Saturate(output[i] + (gain * _ring[(read + i) & _mask]));
        }

        Volatile.Write(ref _read, read + count);
    }
}
