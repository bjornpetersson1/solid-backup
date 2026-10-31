using PortAudioSharp;

using PaStream = PortAudioSharp.Stream;

namespace SolidBackup.Audio.PortAudio;

/// <summary>A mono input-only stream on one device.</summary>
public sealed class PortAudioInput(PortAudioDevice device, int sampleRate, int bufferSize) : IAudioInput
{
    private const string DeviceName = "Ljudingången";

    private PaStream? _stream;
    private IAudioInputHandler? _handler;
    private long _overflows;

    public int SampleRate => sampleRate;

    public int BufferSize => bufferSize;

    public long InputOverflows => Interlocked.Read(ref _overflows);

    public void Start(IAudioInputHandler handler)
    {
        if (_stream is not null)
        {
            throw new InvalidOperationException("The input is already started.");
        }

        _handler = handler;
        _stream = PortAudioStreams.Open(
            PortAudioStreams.Parameters(device, 1), null, sampleRate, bufferSize, OnAudio, DeviceName);
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
        if ((statusFlags & StreamCallbackFlags.InputOverflow) != 0)
        {
            Interlocked.Increment(ref _overflows);
        }

        _handler?.OnBuffer(new ReadOnlySpan<float>((void*)input, (int)frameCount));
        return StreamCallbackResult.Continue;
    }
}
