using SolidBackup.Analysis;
using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Live;
using SolidBackup.Recording;
using SolidBackup.Tests.Audio;
using SolidBackup.Tests.Audio.Devices;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public class LiveCommandTests
{
    private static readonly AppSettings Settings = TestSettings.Default;

    private static readonly LiveSnapshot Playing = new(
        new LiveState(new PitchEstimate(0, HasNote: true, 6912, Confidence: 1), -24, GateOpen: true, new Chord(9, ChordQuality.Minor), new Key(0, Mode.Ionian)),
        ["gate", "dynamics"],
        "Held",
        new LiveLatency(6, 30.5, 1.3333, 5),
        new AudioCounters(1, 2, 0, 0, 0));

    [Theory]
    [InlineData(ConsoleKey.Q, 'q')]
    [InlineData(ConsoleKey.Escape, '\u001b')]
    public void StopKey_StopsTheAudioAndPrintsDropoutsAndLatency(ConsoleKey key, char keyChar)
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(new ConsoleKeyInfo(keyChar, key, shift: false, alt: false, control: false));
        FakeDevices devices = new();

        int exitCode = Command(console, devices).Run(new SettingOverrides());

        Assert.Equal(0, exitCode);
        Assert.Contains(LiveCommand.RunningMessage, console.Output);
        Assert.Contains("Live-läget stoppat. Avbrott: 3 (överflöden i ingången: 1, underflöden i utgången: 2).", console.Output);
        Assert.Contains("ingång 6.0 + kedja 34.0 + överlämning 1.3 + utgång 5.0 ms", console.Output);
        Assert.True(devices.Input.Started && devices.Input.Stopped);
        Assert.True(devices.Output.Started && devices.Output.Stopped && devices.Output.Rendered > 0);
        Assert.True(devices.Disposed);
    }

    [Fact]
    public void Cancellation_StopsLikeAStopKey()
    {
        TestConsole console = TestConsoles.Wide();
        FakeDevices devices = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        int exitCode = Command(console, devices).Run(new SettingOverrides(), cancellation: cancellation.Token);

        Assert.Equal(0, exitCode);
        Assert.Contains("Live-läget stoppat.", console.Output);
        Assert.True(devices.Input.Stopped && devices.Output.Stopped && devices.Disposed);
    }

    [Fact]
    public void Cancellation_WhileRecording_FinishesTheFileAndClosesTheDevices()
    {
        TestConsole console = TestConsoles.Wide();
        KeptStream stream = new();
        FakeDevices devices = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        LiveCommand command = new(console, () => new(Settings, null), _ => devices, TestPreflight.Create(), _ => ("live.wav", stream));

        int exitCode = command.Run(new SettingOverrides(), record: true, cancellation: cancellation.Token);

        Assert.Equal(0, exitCode);
        Assert.Contains("Inspelningen sparad: live.wav", console.Output);
        Assert.Equal((uint)(Settings.BufferSize * 4), System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(stream.Contents.AsSpan(40)));
        Assert.True(devices.Disposed);
    }

    [Fact]
    public void RecordOnInTheSettings_RecordsWithoutTheFlag()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Q);
        KeptStream stream = new();
        LiveCommand command = new(console, () => new(Settings with { RecordOn = true }, null), _ => new FakeDevices(), TestPreflight.Create(), _ => ("live.wav", stream));

        Assert.Equal(0, command.Run(new SettingOverrides()));
        Assert.Contains(RecordingSession.RecordingMessage("live.wav"), console.Output);
    }

    [Fact]
    public void WhileRunning_ShowsTheStatusLine()
    {
        TestConsole console = TestConsoles.Wide().EmitAnsiSequences();
        console.Input.PushKey(ConsoleKey.Q);

        Command(console, new FakeDevices()).Run(new SettingOverrides());

        Assert.Contains("\rTon – · Ackord – · Tonart – · Latens 46.3 ms (under målet 50 ms) · Avbrott 3\u001b[K", console.Output);
    }

    [Fact]
    public void AStatusLineWiderThanTheTerminal_IsCutSoItCanBeRewritten()
    {
        TestConsole console = TestConsoles.Wide().EmitAnsiSequences();
        console.Profile.Width = 37;
        console.Input.PushKey(ConsoleKey.Q);

        Command(console, new FakeDevices()).Run(new SettingOverrides());

        Assert.Contains("\rTon – · Ackord – · Tonart – · Latens\u001b[K", console.Output);
    }

    [Fact]
    public void NotInteractive_ExitsOneWithoutOpeningAudio()
    {
        TestConsole console = new();
        bool opened = false;

        int exitCode = new LiveCommand(console, () => new(Settings, null), _ =>
        {
            opened = true;
            return new FakeDevices();
        }, TestPreflight.Create()).Run(new SettingOverrides());

        Assert.Equal(1, exitCode);
        Assert.Contains(LiveCommand.NotInteractiveMessage, console.Output);
        Assert.False(opened);
    }

    [Fact]
    public void UnknownFeature_ShowsTheSwedishErrorAndOpensNothing()
    {
        TestConsole console = TestConsoles.Wide();
        FakeDevices devices = new();

        int exitCode = Command(console, devices).Run(new SettingOverrides(Features: "eko"));

        Assert.Equal(1, exitCode);
        Assert.Contains(SettingOverrides.UnknownFeatureMessage("eko"), console.Output);
        Assert.False(devices.Input.Started);
    }

    [Fact]
    public void InvalidOverride_ShowsTheSwedishErrorAndOpensNothing()
    {
        TestConsole console = TestConsoles.Wide();
        FakeDevices devices = new();

        int exitCode = Command(console, devices).Run(new SettingOverrides(Style: "Polka"));

        Assert.Equal(1, exitCode);
        Assert.Contains("Ogiltigt värde för HarmonyStyle", console.Output);
        Assert.False(devices.Input.Started);
    }

    [Fact]
    public void MicrophoneDenied_OpensNothing()
    {
        TestConsole console = TestConsoles.Wide();
        bool opened = false;

        int exitCode = new LiveCommand(console, () => new(Settings, null), _ =>
        {
            opened = true;
            return new FakeDevices();
        }, TestPreflight.Create(MicrophonePermission.Denied)).Run(new SettingOverrides());

        Assert.Equal(1, exitCode);
        Assert.False(opened);
    }

    [Fact]
    public void BuiltInSpeakers_WarnsAndStillRuns()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Escape);
        AudioPreflight preflight = TestPreflight.Create(routes: new FakeRoutes(TestPreflight.Speakers));

        int exitCode = new LiveCommand(console, () => new(Settings, null), _ => new FakeDevices(), preflight).Run(new SettingOverrides());

        Assert.Equal(0, exitCode);
        Assert.Contains(AudioPreflight.SpeakerWarning, console.Output);
    }

    [Fact]
    public void Record_WritesTheInputAndTheOutput()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Q);
        KeptStream stream = new();
        LiveCommand command = new(console, () => new(Settings, null), _ => new FakeDevices(), TestPreflight.Create(), _ => ("live.wav", stream));

        int exitCode = command.Run(new SettingOverrides(), record: true);

        Assert.Equal(0, exitCode);
        Assert.Contains(RecordingSession.RecordedMessage("live.wav", Settings.BufferSize / (double)Settings.SampleRate, 0), console.Output);
        Assert.Equal(44 + (Settings.BufferSize * 4), stream.Contents.Length);
    }

    [Fact]
    public void Play_RunsValidatedSettingsWithoutLoadingAny()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Q);
        LiveCommand command = new(console, () => throw new InvalidOperationException("Play loads nothing."), _ => new FakeDevices(), TestPreflight.Create());

        Assert.Equal(new LiveExit(0, Interrupted: false), command.Play(Settings with { Sound = "Organ" }));
    }

    [Fact]
    public void Play_StoppedByCtrlC_SaysItWasInterrupted()
    {
        TestConsole console = TestConsoles.Wide();
        LiveCommand command = new(console, () => new(Settings, null), _ => new FakeDevices(), TestPreflight.Create());
        using CancellationTokenSource ctrlC = new();
        ctrlC.Cancel();

        Assert.Equal(new LiveExit(0, Interrupted: true), command.Play(Settings, cancellation: ctrlC.Token));
    }

    [Fact]
    public void StatusLine_ShowsNoteChordKeyLatencyAndDropouts()
    {
        Assert.Equal(
            "Ton A4 +12 cent · Ackord Am · Tonart C-dur · Latens 42.8 ms (under målet 50 ms) · Avbrott 3",
            LiveCommand.StatusLine(Playing, 50));
    }

    [Fact]
    public void LatencyText_SaysWhenTheTargetIsMissed()
    {
        Assert.Equal("42.8 ms (över målet 40 ms)", LiveCommand.LatencyText(Playing.Latency, 40));
        Assert.Equal("50.0 ms (över målet 50 ms)", LiveCommand.LatencyText(new LiveLatency(10, 30, 5, 5), 50));
    }

    [Fact]
    public void UnknownChainDelay_IsShownAsAQuestionMark()
    {
        LiveLatency unknown = new(6, null, 1.3, 5);

        Assert.Equal("? ms (målet 50 ms)", LiveCommand.LatencyText(unknown, 50));
        Assert.Equal("ingång 6.0 + kedja ? + överlämning 1.3 + utgång 5.0 ms", LiveCommand.PartsText(unknown));
    }

    private static LiveCommand Command(TestConsole console, FakeDevices devices)
    {
        return new LiveCommand(console, () => new(Settings, null), _ => devices, TestPreflight.Create());
    }
}
