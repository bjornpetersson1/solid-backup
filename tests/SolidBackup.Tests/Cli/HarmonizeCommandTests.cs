using SolidBackup.Analysis;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Measurement;
using SolidBackup.Tests.Analysis;
using SolidBackup.Tests.Audio;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public sealed class HarmonizeCommandTests : IDisposable
{
    // The default key is auto; most tests use a fixed key so the chords follow from the first note.
    private static readonly AppSettings FixedKeySettings = TestSettings.Default with { Key = "C" };

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "solid-backup-tests", Guid.NewGuid().ToString("N"));
    private readonly string _wavPath;

    public HarmonizeCommandTests()
    {
        Directory.CreateDirectory(_folder);
        _wavPath = Path.Combine(_folder, "melodi.wav");

        // C4 then B4, half a second each: C, then G in C major.
        float[] samples = [.. Tone(60, TestSignal.SampleRate), .. Tone(71, TestSignal.SampleRate)];
        File.WriteAllBytes(_wavPath, TestWav.Pcm16(TestSignal.SampleRate, 1, samples));
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Melody_PrintsHeaderAndEveryChordChange()
    {
        (int exitCode, string[] lines) = Run();

        Assert.Equal(0, exitCode);
        Assert.Equal($"{_wavPath}: harmonistil Kyrko, tonart C-dur", lines[0]);
        Assert.Equal(["C4   C", "B4   G"], ChangesWithoutTimes(lines));
    }

    [Fact]
    public void Overrides_ChangeStyleKeyAndModeForThisRun()
    {
        (int exitCode, string[] lines) = Run(style: "Drone", key: "Bb", mode: "Dorian");

        Assert.Equal(0, exitCode);
        Assert.Equal($"{_wavPath}: harmonistil Bordun, tonart Bb dorisk", lines[0]);
        Assert.Single(lines[1..]);
        Assert.EndsWith(" Bb5", lines[1]);
    }

    [Theory]
    [InlineData("Pop", "harmonistil Pop", new[] { "C4   C", "B4   G" })]
    [InlineData("Modal", "harmonistil Modal", new[] { "C4   C" })]
    public void PopAndModal_PrintTheirChords(string style, string header, string[] changes)
    {
        (int exitCode, string[] lines) = Run(style: style);

        Assert.Equal(0, exitCode);
        Assert.Contains(header, lines[0]);
        Assert.Equal(changes, ChangesWithoutTimes(lines));
    }

    [Fact]
    public void Blues_PrintsAKeyMove()
    {
        // C4 then Ab4: C7, then the blues key moves to F for Bb7.
        float[] samples = [.. Tone(60, TestSignal.SampleRate), .. Tone(68, TestSignal.SampleRate)];
        File.WriteAllBytes(_wavPath, TestWav.Pcm16(TestSignal.SampleRate, 1, samples));

        (int exitCode, string[] lines) = Run(style: "Blues");

        Assert.Equal(0, exitCode);
        Assert.Equal(["C4   C7", "tonart F-dur", "Ab4  Bb7"], ChangesWithoutTimes(lines));
    }

    [Theory]
    [InlineData("C")]
    [InlineData("auto")]
    public void Free_PrintsChordsAndNoKey(string key)
    {
        (int exitCode, string[] lines) = Run(style: "Free", settings: FixedKeySettings with { Key = key });

        Assert.Equal(0, exitCode);
        Assert.Equal($"{_wavPath}: harmonistil Fri, tonart –", lines[0]);
        Assert.Equal(["C4   C", "B4   Em"], ChangesWithoutTimes(lines));
    }

    [Fact]
    public void KeyOrModeWithFree_WarnsThatTheyAreIgnored()
    {
        (int exitCode, string[] lines) = Run(style: "Free", key: "D", mode: "Dorian");

        Assert.Equal(0, exitCode);
        Assert.Equal(HarmonySetup.KeyIgnoredMessage, lines[0]);
    }

    [Fact]
    public void Silence_SaysNoNotesWereFound()
    {
        File.WriteAllBytes(_wavPath, TestWav.Pcm16(TestSignal.SampleRate, 1, TestSignal.Silence(0.5)));

        (int exitCode, string[] lines) = Run();

        Assert.Equal(0, exitCode);
        Assert.Equal(HarmonizeCommand.NoNotesMessage, lines[^1]);
    }

    [Fact]
    public void AutoKey_PrintsTheKeyBeforeTheFirstChord()
    {
        CorpusClip melody = GeneratedCorpus.Melodies(TestSignal.SampleRate).Single(clip => clip.Name == "generated-melody-a-minor");
        File.WriteAllBytes(_wavPath, TestWav.Pcm16(TestSignal.SampleRate, 1, melody.Samples));

        // The default settings, whose key is auto.
        (int exitCode, string[] lines) = Run(settings: TestSettings.Default);

        Assert.Equal(0, exitCode);
        Assert.Equal($"{_wavPath}: harmonistil Kyrko, tonart auto", lines[0]);
        Assert.EndsWith(" s  tonart A-moll", lines[1]);
        Assert.Equal(lines[1].Split(' ')[0], lines[2].Split(' ')[0]);
        Assert.Single(lines, line => line.Contains("tonart A-moll", StringComparison.Ordinal));
        Assert.DoesNotContain(lines[2..], line => line.Contains("tonart", StringComparison.Ordinal));
    }

    [Fact]
    public void AutoKeyOnAShortClip_SaysNoKeyWasFound()
    {
        (int exitCode, string[] lines) = Run(key: HarmonySettings.AutoKey);

        Assert.Equal(0, exitCode);
        Assert.Equal([$"{_wavPath}: harmonistil Kyrko, tonart auto", HarmonizeCommand.NoKeyMessage], lines);
    }

    [Fact]
    public void ModeWithAutoKey_WarnsThatItIsIgnored()
    {
        (int exitCode, string[] lines) = Run(key: HarmonySettings.AutoKey, mode: "Dorian");

        Assert.Equal(0, exitCode);
        Assert.Equal(HarmonySetup.ModeIgnoredMessage, lines[0]);
    }

    [Fact]
    public void InvalidOverride_NamesTheSetting()
    {
        AssertFails("Ogiltigt värde för Key: \"H\" är ingen giltig tonart; ange en grundton som C, F# eller Bb, eller auto.", key: "H");
    }

    [Theory]
    [InlineData("saknas.wav")]
    [InlineData("")]
    public void MissingFile_FailsWithSwedishMessage(string name)
    {
        string missing = name.Length == 0 ? "" : Path.Combine(_folder, name);

        (int exitCode, string[] lines) = Run(path: missing);

        Assert.Equal(1, exitCode);
        Assert.Equal([RecordingFiles.MissingFileMessage(missing)], lines);
    }

    [Fact]
    public void UnsupportedSampleRate_FailsWithSwedishMessage()
    {
        File.WriteAllBytes(_wavPath, TestWav.Pcm16(8_000, 1, new float[8_000]));

        (int exitCode, string[] lines) = Run();

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Ogiltigt värde för SampleRate: 8000 Hz stöds inte", lines[^1]);
    }

    [Fact]
    public void FileAt441kHz_UsesItsOwnSampleRate()
    {
        float[] samples = [.. Tone(60, 44_100), .. Tone(71, 44_100)];
        File.WriteAllBytes(_wavPath, TestWav.Pcm16(44_100, 1, samples));

        (int exitCode, string[] lines) = Run();

        Assert.Equal(0, exitCode);
        Assert.Equal(["C4   C", "B4   G"], ChangesWithoutTimes(lines));
    }

    [Fact]
    public void ChangeLine_SpellsTheNoteLikeTheChord()
    {
        Assert.EndsWith("Db4  Db", HarmonizeCommand.ChangeLine(new PitchEstimate(1250, true, 6100, 1), new Chord(1, ChordQuality.Major)));
    }

    [Fact]
    public void FileThatIsNotWav_FailsWithSwedishMessage()
    {
        File.WriteAllText(_wavPath, "inte ljud");

        (int exitCode, string[] lines) = Run();

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"Filen {_wavPath} kunde inte läsas: ", lines[^1]);
    }

    // Half a second of a sine at amplitude 0.5.
    private static float[] Tone(int midi, int sampleRate)
    {
        return TestSignal.Sine(midi, 0.5, 0.5, sampleRate);
    }

    private static string[] ChangesWithoutTimes(string[] lines)
    {
        return [.. lines[1..].Select(line => line[(line.IndexOf("s  ", StringComparison.Ordinal) + 3)..])];
    }

    [Fact]
    public void Jazz_PrintsTheLoopsChangesInSilenceWithoutANote()
    {
        File.WriteAllBytes(_wavPath, TestWav.Pcm16(TestSignal.SampleRate, 1, [.. TestSignal.Sine(60, 0.3, 0.5), .. TestSignal.Silence(2.5)]));

        TestConsole console = TestConsoles.Wide();
        int exitCode = new HarmonizeCommand(console, () => new SettingsLoadResult(FixedKeySettings, null))
            .Run(_wavPath, "Jazz", "C", rhythm: new RhythmOverrides(Bpm: 120));
        string[] lines = console.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd()).ToArray();

        Assert.Equal(0, exitCode);
        Assert.EndsWith("Cmaj7", lines[1]);
        Assert.Contains(lines, line => line.EndsWith("–    Am7"));
        Assert.Contains(lines, line => line.EndsWith("–    Dm7"));
    }

    [Fact]
    public void ChangeLine_WithoutANote_ShowsADash()
    {
        Assert.Equal("1.00 s  –    G7", HarmonizeCommand.ChangeLine(PitchEstimate.NoNote(1000, 0), new Chord(7, ChordQuality.Dominant7)));
    }

    private void AssertFails(string message, string? style = null, string? key = null)
    {
        (int exitCode, string[] lines) = Run(style: style, key: key);

        Assert.Equal(1, exitCode);
        Assert.Equal([message], lines);
    }

    private (int ExitCode, string[] Lines) Run(string? path = null, string? style = null, string? key = null, string? mode = null, AppSettings? settings = null)
    {
        TestConsole console = TestConsoles.Wide();
        int exitCode = new HarmonizeCommand(console, () => new SettingsLoadResult(settings ?? FixedKeySettings, null)).Run(path ?? _wavPath, style, key, mode);
        return (exitCode, console.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd()).ToArray());
    }
}
