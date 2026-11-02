using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Tests.Audio;
using SolidBackup.Tests.Audio.Devices;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public class PassThroughCommandTests
{
    private static readonly AppSettings Settings = TestSettings.Default;

    private static readonly AudioCounters Counters = FakeDevices.Counters;

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
    public void MicrophoneNotAskedYet_ShowsTheNoticeAndRuns()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Escape);

        int exitCode = Command(console, _ => new FakeDevices(), TestPreflight.Create(MicrophonePermission.NotDetermined)).Run();

        Assert.Equal(0, exitCode);
        Assert.Contains(AudioPreflight.FirstUseNotice, console.Output);
        Assert.Contains(PassThroughCommand.RunningMessage, console.Output);
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

    [Fact]
    public void Record_SaysWhereItRecordsAndHowLongTheRecordingIs()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Q);
        KeptStream stream = new();
        FakeDevices devices = new();
        PassThroughCommand command = new(console, () => new(Settings, null), _ => devices, TestPreflight.Create(), _ => ("inspelning.wav", stream));

        int exitCode = command.Run(record: true);

        Assert.Equal(0, exitCode);
        Assert.Contains(RecordingSession.RecordingMessage("inspelning.wav"), console.Output);
        Assert.Contains(RecordingSession.RecordedMessage("inspelning.wav", Settings.BufferSize / (double)Settings.SampleRate, 0), console.Output);
        Assert.Equal(44 + (Settings.BufferSize * 4), stream.Contents.Length);
    }

    [Fact]
    public void Record_FolderCannotBeWritten_FailsBeforeTheAudioStarts()
    {
        TestConsole console = TestConsoles.Wide();
        FakeDevices devices = new();
        PassThroughCommand command = new(
            console, () => new(Settings, null), _ => devices, TestPreflight.Create(), _ => throw new UnauthorizedAccessException("nej"));

        int exitCode = command.Run(record: true);

        Assert.Equal(1, exitCode);
        Assert.Contains(RecordingSession.RecordingFolderMessage(Settings.RecordingFolder), console.Output);
        Assert.False(devices.Input.Started);
        Assert.True(devices.Disposed);
    }

    [Fact]
    public void Record_AudioFailsToStart_LeavesNoFileAndClaimsNoRecording()
    {
        TestConsole console = TestConsoles.Wide();
        string path = Path.Combine(Path.GetTempPath(), $"solid-backup-{Guid.NewGuid():N}.wav");
        PassThroughCommand command = new(
            console, () => new(Settings, null), _ => new FakeDevices(failOnStart: true), TestPreflight.Create(), _ => (path, File.Create(path)));

        int exitCode = command.Run(record: true);

        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(path));
        Assert.DoesNotContain("Inspelningen sparad", console.Output);
    }

    [Fact]
    public void Record_HeaderCannotBeWritten_ClosesAndRemovesTheFile()
    {
        TestConsole console = TestConsoles.Wide();
        string path = Path.Combine(Path.GetTempPath(), $"solid-backup-{Guid.NewGuid():N}.wav");
        ReadOnlyFile stream = new(path);
        FakeDevices devices = new();
        PassThroughCommand command = new(console, () => new(Settings, null), _ => devices, TestPreflight.Create(), _ => (path, stream));

        int exitCode = command.Run(record: true);

        Assert.Equal(1, exitCode);
        Assert.True(stream.Disposed);
        Assert.False(File.Exists(path));
        Assert.DoesNotContain(RecordingSession.RecordingMessage(path), console.Output);
        Assert.False(devices.Input.Started);
    }

    [Fact]
    public void Record_AudioFailsToStop_StillFinishesTheFile()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Q);
        KeptStream stream = new();
        PassThroughCommand command = new(
            console, () => new(Settings, null), _ => new FakeDevices(failOnStop: true), TestPreflight.Create(), _ => ("inspelning.wav", stream));

        command.Run(record: true);

        Assert.Contains("Inspelningen sparad: inspelning.wav", console.Output);
        Assert.Equal((uint)(Settings.BufferSize * 4), System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(stream.Contents.AsSpan(40)));
    }

    [Fact]
    public void WithoutRecord_CreatesNoRecording()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Q);
        PassThroughCommand command = new(
            console, () => new(Settings, null), _ => new FakeDevices(), TestPreflight.Create(), _ => throw new InvalidOperationException("No recording was asked for."));

        Assert.Equal(0, command.Run());
    }

    private static PassThroughCommand Command(
        TestConsole console, Func<AppSettings, IAudioDevices> openDevices, AudioPreflight? preflight = null)
    {
        return new PassThroughCommand(console, () => new(Settings, null), openDevices, preflight ?? TestPreflight.Create());
    }

    // A real file that refuses the header, as a full disk would.
    private sealed class ReadOnlyFile(string path) : FileStream(path, FileMode.Create)
    {
        public bool Disposed { get; private set; }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new IOException("Disken är full.");
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
