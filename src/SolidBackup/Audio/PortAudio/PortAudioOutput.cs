using PortAudioSharp;

using PaStream = PortAudioSharp.Stream;

namespace SolidBackup.Audio.PortAudio;

/// <summary>
/// A stereo output-only stream on one device. Buffers written from the input's thread pass
/// through a <see cref="HandOverBuffer"/> to this stream's callback, which plays the mono
/// signal on both channels.
/// </summary>
public sealed class PortAudioOutput(PortAudioDevice device, int sampleRate, int bufferSize) : IAudioOutput
{
    private const string DeviceName = "Ljudutgången";
    private const int Channels = 2;

    private readonly HandOverBuffer _handOver = new(sampleRate, bufferSize);
    private readonly float[] _mono = new float[bufferSize];
    private PaStream? _stream;
    private long _underflows;

    public int SampleRate => sampleRate;

    public int BufferSize => bufferSize;

    public long OutputUnderflows => Interlocked.Read(ref _underflows);

    public long HandOverUnderruns => _handOver.Underruns;

    public long HandOverOverruns => _handOver.Overruns;

    public long DriftCorrections => _handOver.DriftCorrections;

    public void Start()
    {
        if (_stream is not null)
        {
            throw new InvalidOperationException("The output is already started.");
        }

        _handOver.Reset();
        _stream = PortAudioStreams.Open(
            null, PortAudioStreams.Parameters(device, Channels), sampleRate, bufferSize, OnAudio, DeviceName);
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        _handOver.Write(samples);
    }

    public void Stop()
    {
        PortAudioStreams.Close(ref _stream, DeviceName);
    }

    public void Dispose()
    {
        Stop();
    }

    private unsafe StreamCallbackResult OnAudio(
        IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        if ((statusFlags & StreamCallbackFlags.OutputUnderflow) != 0)
        {
            Interlocked.Increment(ref _underflows);
        }

        // PortAudio asks for bufferSize frames, as the stream was opened with; anything beyond that is silence.
        Span<float> stereo = new((void*)output, (int)frameCount * Channels);
        Span<float> mono = _mono.AsSpan(0, Math.Min((int)frameCount, _mono.Length));
        _handOver.Read(mono);
        for (int frame = 0; frame < mono.Length; frame++)
        {
            stereo[Channels * frame] = mono[frame];
            stereo[(Channels * frame) + 1] = mono[frame];
        }

        stereo[(mono.Length * Channels)..].Clear();

        return StreamCallbackResult.Continue;
    }
}
