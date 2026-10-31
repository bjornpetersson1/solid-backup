using SolidBackup.Audio;
using SolidBackup.Cli;
using SolidBackup.Configuration;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public class PassThroughCommandTests
{
    private static readonly AppSettings Settings = new()
    {
        LowestNote = "E2",
        HighestNote = "C6",
        BufferSize = 64,
        SampleRate = 48_000,
        GateThresholdDbfs = -50,
        LatencyTargetMs = 50,
    };

    private static readonly AudioCounters Counters = new(1, 2, 3, 4, 5);

    [Theory]
    [InlineData(ConsoleKey.Q, 'q')]
    [InlineData(ConsoleKey.Q, 'Q')]
    [InlineData(ConsoleKey.Escape, '\u001b')]
    public void StopKey_StopsAudioPrintsCountsAndExitsZero(ConsoleKey key, char keyChar)
    {
        TestConsole console = WideConsole();
        console.Input.PushKey(ConsoleKey.A);
        console.Input.PushKey(new ConsoleKeyInfo(keyChar, key, shift: false, alt: false, control: false));
        FakeDevices devices = new();

        int exitCode = new PassThroughCommand(console, () => new(Settings, null), _ => devices).Run();

        Assert.Equal(0, exitCode);
        Assert.Contains(PassThroughCommand.RunningMessage, console.Output);
        Assert.Contains(PassThroughCommand.Summary(Counters), console.Output);
        Assert.True(devices.Input.Started && devices.Input.Stopped);
        Assert.True(devices.Output.Started && devices.Output.Stopped);
        Assert.True(devices.Disposed);
    }

    [Fact]
    public void Cancellation_StopsAudioLikeAStopKey()
    {
        TestConsole console = WideConsole();
        FakeDevices devices = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        int exitCode = new PassThroughCommand(console, () => new(Settings, null), _ => devices).Run(cancellation.Token);

        Assert.Equal(0, exitCode);
        Assert.Contains(PassThroughCommand.Summary(Counters), console.Output);
        Assert.True(devices.Input.Stopped && devices.Output.Stopped && devices.Disposed);
    }

    [Fact]
    public void Summary_NamesEveryCountInSwedish()
    {
        Assert.Equal(
            "Medhörningen stoppad. Överflöden i ingången: 1, underflöden i utgången: 2, tomma överlämningar: 3, "
            + "tappade överlämningar: 4, driftkorrigeringar: 5.",
            PassThroughCommand.Summary(Counters));
    }

    [Fact]
    public void DevicesCannotOpen_ShowsSwedishErrorAndExitsOne()
    {
        TestConsole console = WideConsole();

        int exitCode = new PassThroughCommand(
            console, () => new(Settings, null), _ => throw new AudioDeviceException("Det finns ingen ljudingång.")).Run();

        Assert.Equal(1, exitCode);
        Assert.Contains("Ljudfel: Det finns ingen ljudingång.", console.Output);
    }

    [Fact]
    public void StreamCannotStart_ClosesDevicesAndExitsOne()
    {
        TestConsole console = WideConsole();
        FakeDevices devices = new(failOnStart: true);

        int exitCode = new PassThroughCommand(console, () => new(Settings, null), _ => devices).Run();

        Assert.Equal(1, exitCode);
        Assert.Contains("Ljudfel: Ljudingången kunde inte öppnas.", console.Output);
        Assert.True(devices.Disposed);
    }

    [Fact]
    public void StreamCannotStop_ShowsSwedishErrorClosesDevicesAndExitsOne()
    {
        TestConsole console = WideConsole();
        console.Input.PushKey(ConsoleKey.Escape);
        FakeDevices devices = new(failOnStop: true);

        int exitCode = new PassThroughCommand(console, () => new(Settings, null), _ => devices).Run();

        Assert.Equal(1, exitCode);
        Assert.Contains("Ljudfel: Ljudingången kunde inte stängas.", console.Output);
        Assert.True(devices.Disposed);
    }

    [Fact]
    public void SettingsMessage_IsShownBeforeStarting()
    {
        TestConsole console = WideConsole();
        console.Input.PushKey(ConsoleKey.Escape);

        new PassThroughCommand(console, () => new(Settings, "Inställningsfilen kunde inte läsas."), _ => new FakeDevices()).Run();

        Assert.Contains("Inställningsfilen kunde inte läsas.", console.Output);
    }

    [Fact]
    public void NotInteractive_ExitsOneWithoutOpeningAudio()
    {
        TestConsole console = new();
        bool opened = false;

        int exitCode = new PassThroughCommand(console, () => new(Settings, null), _ =>
        {
            opened = true;
            return new FakeDevices();
        }).Run();

        Assert.Equal(1, exitCode);
        Assert.Contains(PassThroughCommand.NotInteractiveMessage, console.Output);
        Assert.False(opened);
    }

    // Wide enough that no message wraps.
    private static TestConsole WideConsole()
    {
        TestConsole console = new TestConsole().Interactive();
        console.Profile.Width = 400;
        return console;
    }

    private sealed class FakeDevices(bool failOnStart = false, bool failOnStop = false) : IAudioDevices
    {
        public FakeInput Input { get; } = new(failOnStart, failOnStop);

        public FakeOutput Output { get; } = new();

        public bool Disposed { get; private set; }

        IAudioInput IAudioDevices.Input => Input;

        IAudioOutput IAudioDevices.Output => Output;

        public AudioCounters Counters => PassThroughCommandTests.Counters;

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class FakeInput(bool failOnStart, bool failOnStop) : IAudioInput
    {
        public int SampleRate => Settings.SampleRate;

        public int BufferSize => Settings.BufferSize;

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

    private sealed class FakeOutput : IAudioOutput
    {
        public int SampleRate => Settings.SampleRate;

        public int BufferSize => Settings.BufferSize;

        public bool Started { get; private set; }

        public bool Stopped { get; private set; }

        public void Start()
        {
            Started = true;
        }

        public void Write(ReadOnlySpan<float> samples)
        {
        }

        public void Stop()
        {
            Stopped = true;
        }

        public void Dispose()
        {
        }
    }
}
