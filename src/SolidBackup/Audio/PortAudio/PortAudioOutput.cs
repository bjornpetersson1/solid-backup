using PortAudioSharp;

using SolidBackup.Audio.Devices;

using PaStream = PortAudioSharp.Stream;

namespace SolidBackup.Audio.PortAudio;

/// <summary>
/// An output-only stream on one device, stereo (or mono on a one-channel device). Buffers
/// written from the input's thread pass through a <see cref="HandOverBuffer"/> to this
/// stream's callback, which plays the mono signal on every channel.
/// </summary>
public sealed class PortAudioOutput(AudioDeviceInfo device, int sampleRate, int bufferSize) : IAudioOutput
{
    public const string DeviceName = "Ljudutgången";

    private readonly int _channels = PortAudioStreams.OutputChannels(device);

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
            null, PortAudioStreams.Parameters(device, AudioDirection.Output, _channels), sampleRate, bufferSize, OnAudio, DeviceName);
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
        Span<float> interleaved = new((void*)output, (int)frameCount * _channels);
        Span<float> mono = _mono.AsSpan(0, Math.Min((int)frameCount, _mono.Length));
        _handOver.Read(mono);
        PortAudioStreams.FanOut(mono, interleaved, _channels);

        return StreamCallbackResult.Continue;
    }
}
