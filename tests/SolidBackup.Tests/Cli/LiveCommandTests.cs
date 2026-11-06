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
        Assert.Contains(LiveDashboard.Footer(new DashboardContext(ReturnsToMenu: false, OnSpeakers: false)), console.Output);
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
    public void WhileRunning_ShowsTheDashboard()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Q);

        Command(console, new FakeDevices()).Run(new SettingOverrides());

        Assert.Contains(LiveDashboard.Title, console.Output);
        Assert.Contains("46.3 ms", console.Output);
        Assert.Contains("q/Esc: avsluta", console.Output);
    }

    [Fact]
    public void FromTheMenu_TheDashboardSaysQAndEscLeadBack()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Q);

        new LiveCommand(console, () => new(Settings, null), _ => new FakeDevices(), TestPreflight.Create(), returnsToMenu: true).Play(Settings);

        Assert.Contains("q/Esc: tillbaka till menyn", console.Output);
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
    public void BuiltInSpeakersWithMonitoringOn_SaysTheMonitoringIsOff()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Escape);
        AudioPreflight preflight = TestPreflight.Create(routes: new FakeRoutes(TestPreflight.Speakers));

        int exitCode = new LiveCommand(console, () => new(Settings with { MonitorOn = true }, null), _ => new FakeDevices(), preflight).Run(new SettingOverrides());

        Assert.Equal(0, exitCode);
        Assert.Contains(SettingTexts.MonitorOffUnsafeOutput, console.Output);
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
        Assert.Contains(RecordingSession.RecordedMessage("live.wav", Settings.BufferSize / (double)Settings.SampleRate, 0, 0), console.Output);
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
    public void Summary_GivesTheDropoutsAndTheLatencyParts()
    {
        LiveSnapshot stopped = TestSnapshots.Playing with { Counters = new AudioCounters(1, 2, 0, 0, 0) };

        Assert.Equal(
            "Live-läget stoppat. Avbrott: 3 (överflöden i ingången: 1, underflöden i utgången: 2). Latens: 42.8 ms (under målet 50 ms), "
            + "ingång 6.0 + kedja 30.5 + överlämning 1.3 + utgång 5.0 ms.",
            LiveCommand.Summary(stopped));
    }

    [Fact]
    public void Summary_SaysWhenTheTargetIsMissed()
    {
        LiveSnapshot slow = TestSnapshots.Playing with { Latency = new LiveLatency(10, 30, 5, 5) };

        Assert.Contains("Latens: 50.0 ms (över målet 50 ms)", LiveCommand.Summary(slow));
    }

    [Fact]
    public void Summary_ShowsAnUnknownChainDelayAsAQuestionMark()
    {
        LiveSnapshot unknown = TestSnapshots.Playing with { Latency = new LiveLatency(6, null, 1.3, 5) };

        Assert.Contains("Latens: ? ms (målet 50 ms), ingång 6.0 + kedja ? + överlämning 1.3 + utgång 5.0 ms.", LiveCommand.Summary(unknown));
    }

    private static LiveCommand Command(TestConsole console, FakeDevices devices)
    {
        return new LiveCommand(console, () => new(Settings, null), _ => devices, TestPreflight.Create());
    }
}
