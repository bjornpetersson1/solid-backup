using SolidBackup.Audio;

namespace SolidBackup.Tests.Cli;

/// <summary>
/// Devices for the commands that play live audio: the input delivers one silent buffer when it starts, and an output
/// started with a renderer renders one buffer right after it, as the two callbacks would.
/// </summary>
internal sealed class FakeDevices : IAudioDevices
{
    public static readonly AudioCounters Counters = new(1, 2, 3, 4, 5);

    public static readonly ReportedLatency ReportedLatency = new(6, 5);

    public FakeDevices(bool failOnStart = false, bool failOnStop = false, int bufferSize = 64, int sampleRate = 48_000)
    {
        Output = new FakeOutput(bufferSize, sampleRate);
        Input = new FakeInput(bufferSize, sampleRate, failOnStart, failOnStop, Output.RenderOne);
    }

    public FakeInput Input { get; }

    public FakeOutput Output { get; }

    public bool Disposed { get; private set; }

    IAudioInput IAudioDevices.Input => Input;

    IAudioOutput IAudioDevices.Output => Output;

    public string OutputDeviceName => "Externa hörlurar";

    AudioCounters IAudioDevices.Counters => Counters;

    public ReportedLatency Latency => Input.Started ? ReportedLatency : default;

    public void Dispose()
    {
        Disposed = true;
    }
}

internal sealed class FakeInput(int bufferSize, int sampleRate, bool failOnStart, bool failOnStop, Action afterBuffer) : IAudioInput
{
    public int SampleRate => sampleRate;

    public int BufferSize => bufferSize;

    public bool Started { get; private set; }

    public bool Stopped { get; private set; }

    public void Start(IAudioInputHandler handler)
    {
        if (failOnStart)
        {
            throw new AudioDeviceException("Ljudingången kunde inte öppnas.");
        }

        Started = true;
        handler.OnBuffer(new float[BufferSize]);
        afterBuffer();
    }

    public void Stop()
    {
        if (failOnStop)
        {
            throw new AudioDeviceException("Ljudingången kunde inte stängas.");
        }

        Stopped = true;
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeOutput(int bufferSize, int sampleRate) : IAudioOutput
{
    private IAudioRenderer? _renderer;

    public int SampleRate => sampleRate;

    public int BufferSize => bufferSize;

    public bool Started { get; private set; }

    public bool Stopped { get; private set; }

    public int Rendered { get; private set; }

    public void Start(IAudioRenderer? renderer = null)
    {
        _renderer = renderer;
        Started = true;
    }

    public void Write(ReadOnlySpan<float> samples)
    {
    }

    public void RenderOne()
    {
        if (_renderer is { } renderer)
        {
            renderer.Render(new float[BufferSize]);
            Rendered++;
        }
    }

    public void Stop()
    {
        Stopped = true;
    }

    public void Dispose()
    {
    }
}
