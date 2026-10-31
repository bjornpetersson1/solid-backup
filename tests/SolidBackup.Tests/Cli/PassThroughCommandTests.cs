using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Tests.Audio.Devices;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public class PassThroughCommandTests
{
    private static readonly AppSettings Settings = TestSettings.Default;

    private static readonly AudioCounters Counters = new(1, 2, 3, 4, 5);

    [Theory]
    [InlineData(ConsoleKey.Q, 'q')]
    [InlineData(ConsoleKey.Q, 'Q')]
    [InlineData(ConsoleKey.Escape, '\u001b')]
    public void StopKey_StopsAudioPrintsCountsAndExitsZero(ConsoleKey key, char keyChar)
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.A);
        console.Input.PushKey(new ConsoleKeyInfo(keyChar, key, shift: false, alt: false, control: false));
        FakeDevices devices = new();

        int exitCode = Command(console, _ => devices).Run();

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
        TestConsole console = TestConsoles.Wide();
        FakeDevices devices = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        int exitCode = Command(console, _ => devices).Run(cancellation.Token);

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
        TestConsole console = TestConsoles.Wide();

        int exitCode = Command(console, _ => throw new AudioDeviceException("Det finns ingen ljudingång.")).Run();

        Assert.Equal(1, exitCode);
        Assert.Contains("Ljudfel: Det finns ingen ljudingång.", console.Output);
    }

    [Fact]
    public void StreamCannotStart_ClosesDevicesAndExitsOne()
    {
        TestConsole console = TestConsoles.Wide();
        FakeDevices devices = new(failOnStart: true);

        int exitCode = Command(console, _ => devices).Run();

        Assert.Equal(1, exitCode);
        Assert.Contains("Ljudfel: Ljudingången kunde inte öppnas.", console.Output);
        Assert.True(devices.Disposed);
    }

    [Fact]
    public void StreamCannotStop_ShowsSwedishErrorClosesDevicesAndExitsOne()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Escape);
        FakeDevices devices = new(failOnStop: true);

        int exitCode = Command(console, _ => devices).Run();

        Assert.Equal(1, exitCode);
        Assert.Contains("Ljudfel: Ljudingången kunde inte stängas.", console.Output);
        Assert.True(devices.Disposed);
    }

    [Fact]
    public void MicrophoneDenied_ShowsInstructionsAndOpensNothing()
    {
        TestConsole console = TestConsoles.Wide();
        bool opened = false;

        int exitCode = Command(
            console,
            _ =>
            {
                opened = true;
                return new FakeDevices();
            },
            TestPreflight.Create(MicrophonePermission.Denied)).Run();

        Assert.Equal(1, exitCode);
        Assert.Contains("Terminal har inte tillgång till mikrofonen.", console.Output);
        Assert.False(opened);
    }

    [Fact]
    public void BuiltInSpeakers_WarnsAndStillRuns()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Escape);
        AudioPreflight preflight = TestPreflight.Create(routes: new FakeRoutes(TestPreflight.Speakers));

        int exitCode = Command(console, _ => new FakeDevices(), preflight).Run();

        Assert.Equal(0, exitCode);
        Assert.Contains(AudioPreflight.SpeakerWarning, console.Output);
        Assert.Contains(PassThroughCommand.RunningMessage, console.Output);
    }

    [Fact]
    public void Headphones_GiveNoWarning()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Escape);

        Command(console, _ => new FakeDevices()).Run();

        Assert.DoesNotContain(AudioPreflight.SpeakerWarning, console.Output);
    }

    [Fact]
    public void SettingsMessage_IsShownBeforeStarting()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Escape);

        new PassThroughCommand(
            console, () => new(Settings, "Inställningsfilen kunde inte läsas."), _ => new FakeDevices(), TestPreflight.Create()).Run();

        Assert.Contains("Inställningsfilen kunde inte läsas.", console.Output);
    }

    [Fact]
    public void NotInteractive_ExitsOneWithoutOpeningAudio()
    {
        TestConsole console = new();
        bool opened = false;

        int exitCode = Command(console, _ =>
        {
            opened = true;
            return new FakeDevices();
        }).Run();

        Assert.Equal(1, exitCode);
        Assert.Contains(PassThroughCommand.NotInteractiveMessage, console.Output);
        Assert.False(opened);
    }

    private static PassThroughCommand Command(
        TestConsole console, Func<AppSettings, IAudioDevices> openDevices, AudioPreflight? preflight = null)
    {
        return new PassThroughCommand(console, () => new(Settings, null), openDevices, preflight ?? TestPreflight.Create());
    }

    private sealed class FakeDevices(bool failOnStart = false, bool failOnStop = false) : IAudioDevices
    {
        public FakeInput Input { get; } = new(failOnStart, failOnStop);

        public FakeOutput Output { get; } = new();

        public bool Disposed { get; private set; }

        IAudioInput IAudioDevices.Input => Input;

        IAudioOutput IAudioDevices.Output => Output;

        public string OutputDeviceName => "Externa hörlurar";

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
