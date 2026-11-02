using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Synthesis;
using SolidBackup.Tests.Audio.Devices;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public sealed class SynthCommandTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "solid-backup-tests", Guid.NewGuid().ToString("N"));

    public SynthCommandTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Theory]
    [InlineData(null, "Pad", 600)]
    [InlineData("Organ", "Organ", 50)]
    [InlineData("Pluck", "Pluck", 200)]
    public void Render_WritesTheChordsAndTheReleaseToAWavFile(string? sound, string expectedSound, int releaseMs)
    {
        TestConsole console = TestConsoles.Wide();
        string path = Path.Combine(_folder, "chords.wav");

        int exitCode = Command(console).Render(path, sound, "C Am F", chordMs: 500);

        Assert.Equal(0, exitCode);
        WavAudio audio = WavReader.Read(path);
        Assert.Equal(48_000, audio.SampleRate);
        Assert.Equal((3 * 24_000) + (releaseMs * 48), audio.Samples.Length);
        Assert.Contains(audio.Samples, sample => Math.Abs(sample) > 0.01);
        Assert.Contains(SynthCommand.RenderedMessage(path, audio.Samples.Length / 48_000.0, Enum.Parse<Sound>(expectedSound)), console.Output);
    }

    [Fact]
    public void Render_DefaultChords_PlaysFourChordsOfASecond()
    {
        string path = Path.Combine(_folder, "default.wav");

        int exitCode = Command(TestConsoles.Wide()).Render(path);

        Assert.Equal(0, exitCode);
        Assert.Equal((4 * 48_000) + (600 * 48), WavReader.Read(path).Samples.Length);
    }

    [Theory]
    [InlineData("C Hm", "\"Hm\" är inget känt ackord; skriv t.ex. C, Am, G7 eller Cmaj7(#11).")]
    [InlineData("C C#", "\"C#\" är inget känt ackord; skriv t.ex. C, Am, G7 eller Cmaj7(#11).")]
    [InlineData("   ", SynthCommand.NoChordsMessage)]
    public void Render_BadChords_FailsWithSwedishMessageAndWritesNothing(string chords, string message)
    {
        TestConsole console = TestConsoles.Wide();
        string path = Path.Combine(_folder, "bad.wav");

        int exitCode = Command(console).Render(path, chords: chords);

        Assert.Equal(1, exitCode);
        Assert.Contains(message, console.Output);
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(10_001)]
    public void Render_ChordMsOutOfRange_FailsWithSwedishMessage(int chordMs)
    {
        TestConsole console = TestConsoles.Wide();

        int exitCode = Command(console).Render(Path.Combine(_folder, "x.wav"), chordMs: chordMs);

        Assert.Equal(1, exitCode);
        Assert.Contains(SynthCommand.ChordMsMessage(chordMs), console.Output);
    }

    [Fact]
    public void Render_LongerThanTenMinutes_FailsWithSwedishMessage()
    {
        TestConsole console = TestConsoles.Wide();
        string chords = string.Join(' ', Enumerable.Repeat("C", 61));

        int exitCode = Command(console).Render(Path.Combine(_folder, "long.wav"), chords: chords, chordMs: 10_000);

        Assert.Equal(1, exitCode);
        Assert.Contains(SynthCommand.TooLongMessage, console.Output);
    }

    [Fact]
    public void Render_EmptyPath_FailsWithSwedishMessage()
    {
        TestConsole console = TestConsoles.Wide();

        int exitCode = Command(console).Render("", chords: "C", chordMs: 100);

        Assert.Equal(1, exitCode);
        Assert.Contains("Filen  kunde inte skrivas.", console.Output);
    }

    [Fact]
    public void Render_WithAPattern_PlaysItOnTheGrid()
    {
        string held = Path.Combine(_folder, "held.wav");
        string arpeggio = Path.Combine(_folder, "arpeggio.wav");

        Command(TestConsoles.Wide()).Render(held, "Organ", "C", chordMs: 1000);
        Command(TestConsoles.Wide()).Render(arpeggio, "Organ", "C", chordMs: 1000, new RhythmOverrides("Arpeggio", Bpm: 120));

        float[] heldSamples = WavReader.Read(held).Samples;
        float[] arpeggioSamples = WavReader.Read(arpeggio).Samples;
        Assert.Equal(heldSamples.Length, arpeggioSamples.Length);
        Assert.NotEqual(heldSamples, arpeggioSamples);
    }

    [Fact]
    public void Render_ARepeatedChordWithASoundThatDiesAway_IsPluckedAgain()
    {
        string path = Path.Combine(_folder, "pluck.wav");

        Command(TestConsoles.Wide()).Render(path, "Pluck", "C C", chordMs: 1000);

        float[] samples = WavReader.Read(path).Samples;
        Assert.Contains(samples[48_000..54_000], sample => Math.Abs(sample) > 0.05);
    }

    [Theory]
    [InlineData("Swing", null, null, "Ogiltigt värde för RhythmPattern")]
    [InlineData(null, 300, null, "Ogiltigt värde för Bpm: 300 måste vara mellan 40 och 240")]
    [InlineData(null, null, "6/8", "Ogiltigt värde för TimeSignature")]
    public void Render_InvalidRhythm_FailsWithSwedishMessage(string? pattern, int? bpm, string? timeSignature, string message)
    {
        TestConsole console = TestConsoles.Wide();

        int exitCode = Command(console).Render(Path.Combine(_folder, "x.wav"), rhythm: new RhythmOverrides(pattern, bpm, timeSignature));

        Assert.Equal(1, exitCode);
        Assert.Contains(message, console.Output);
    }

    [Fact]
    public void Render_UnknownSound_FailsWithSwedishMessage()
    {
        TestConsole console = TestConsoles.Wide();

        int exitCode = Command(console).Render(Path.Combine(_folder, "x.wav"), sound: "Piano");

        Assert.Equal(1, exitCode);
        Assert.Contains("Ogiltigt värde för Sound: \"Piano\" är inget känt ljud; välj Pad, Organ, Pluck.", console.Output);
    }

    [Fact]
    public void Render_UnwritablePath_FailsWithSwedishMessage()
    {
        TestConsole console = TestConsoles.Wide();
        string path = Path.Combine(_folder, "missing", "x.wav");

        int exitCode = Command(console).Render(path);

        Assert.Equal(1, exitCode);
        Assert.Contains($"Filen {path} kunde inte skrivas.", console.Output);
    }

    [Fact]
    public void Play_ToTheEnd_PlaysTheRenderedChordsAndStops()
    {
        TestConsole console = TestConsoles.Wide();
        FakePlayback playback = new();

        int exitCode = Command(console, playback).Play(chords: "C G", chordMs: 200);

        Assert.Equal(0, exitCode);
        Assert.Contains(SynthCommand.PlayingMessage, console.Output);
        Assert.Contains(SynthCommand.PlayedSummary(finished: true, underflows: 2), console.Output);
        Assert.Equal((2 * 9600) + (600 * 48), playback.Samples.Length);
        Assert.True(playback.Stopped && playback.Disposed);
    }

    [Fact]
    public void Play_StopKey_StopsBeforeTheEnd()
    {
        TestConsole console = TestConsoles.Wide();
        console.Input.PushKey(ConsoleKey.Q);
        FakePlayback playback = new() { Finishes = false };

        int exitCode = Command(console, playback).Play(chordMs: 1000);

        Assert.Equal(0, exitCode);
        Assert.Contains(SynthCommand.PlayedSummary(finished: false, underflows: 2), console.Output);
        Assert.True(playback.Stopped && playback.Disposed);
    }

    [Fact]
    public void Play_ToSpeakers_Warns()
    {
        TestConsole console = TestConsoles.Wide();
        SynthCommand command = new(
            console,
            () => new(TestSettings.Default, null),
            _ => new FakePlayback(),
            TestPreflight.Create(routes: new FakeRoutes(TestPreflight.Speakers)));

        command.Play(chordMs: 100, chords: "C");

        Assert.Contains(AudioPreflight.SpeakerWarning, console.Output);
    }

    [Fact]
    public void Play_DeviceFails_ShowsAudioErrorAndExitsOne()
    {
        TestConsole console = TestConsoles.Wide();
        SynthCommand command = new(
            console, () => new(TestSettings.Default, null), _ => throw new AudioDeviceException("Ljudutgången kunde inte öppnas."), TestPreflight.Create());

        int exitCode = command.Play();

        Assert.Equal(1, exitCode);
        Assert.Contains("Ljudfel: Ljudutgången kunde inte öppnas.", console.Output);
    }

    [Fact]
    public void Play_NotInteractive_FailsWithSwedishMessage()
    {
        TestConsole console = new();

        int exitCode = Command(console).Play();

        Assert.Equal(1, exitCode);
        Assert.Contains(SynthCommand.NotInteractiveMessage, console.Output);
    }

    private static SynthCommand Command(TestConsole console, FakePlayback? playback = null)
    {
        return new SynthCommand(console, () => new(TestSettings.Default, null), _ => playback ?? new FakePlayback(), TestPreflight.Create());
    }

    private sealed class FakePlayback : IAudioPlayback
    {
        public float[] Samples { get; private set; } = [];

        /// <summary>False for a playback that only ends when it is stopped.</summary>
        public bool Finishes { get; init; } = true;

        public bool Stopped { get; private set; }

        public bool Disposed { get; private set; }

        public string OutputDeviceName => "Externa hörlurar";

        public long Underflows => 2;

        public bool IsFinished => Finishes;

        public void Start(float[] samples)
        {
            Samples = samples;
        }

        public void Stop()
        {
            Stopped = true;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
