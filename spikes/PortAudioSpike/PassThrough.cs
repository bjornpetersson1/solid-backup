using PortAudioSharp;

namespace PortAudioSpike;

/// <summary>
/// Copies the mono input of a duplex stream to both output channels. The callback only
/// touches fields and the buffers PortAudio hands it: no allocation, locks or I/O.
/// </summary>
internal sealed class PassThrough(bool mute)
{
    private long callbacks;
    private long inputOverflows;
    private long outputUnderflows;
    private float peakInput;

    public long Callbacks => Interlocked.Read(ref callbacks);

    public long InputOverflows => Interlocked.Read(ref inputOverflows);

    public long OutputUnderflows => Interlocked.Read(ref outputUnderflows);

    public float PeakInput => Volatile.Read(ref peakInput);

    public unsafe StreamCallbackResult Callback(
        IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        Interlocked.Increment(ref callbacks);
        if ((statusFlags & StreamCallbackFlags.InputOverflow) != 0)
        {
            Interlocked.Increment(ref inputOverflows);
        }

        if ((statusFlags & StreamCallbackFlags.OutputUnderflow) != 0)
        {
            Interlocked.Increment(ref outputUnderflows);
        }

        ReadOnlySpan<float> samples = new((void*)input, (int)frameCount);
        Span<float> stereo = new((void*)output, (int)frameCount * Program.OutputChannels);
        float peak = peakInput;
        for (int frame = 0; frame < samples.Length; frame++)
        {
            float sample = samples[frame];
            peak = MathF.Max(peak, MathF.Abs(sample));
            float played = mute ? 0 : sample;
            stereo[2 * frame] = played;
            stereo[(2 * frame) + 1] = played;
        }

        Volatile.Write(ref peakInput, peak);
        return StreamCallbackResult.Continue;
    }
}
