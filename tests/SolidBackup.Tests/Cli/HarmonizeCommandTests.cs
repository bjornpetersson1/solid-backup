using SolidBackup.Analysis;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Tests.Analysis;
using SolidBackup.Tests.Audio;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public sealed class HarmonizeCommandTests : IDisposable
{
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
        Assert.Equal($"{_wavPath}: harmonistil Kyrko, tonart C jonisk", lines[0]);
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

    [Fact]
    public void Silence_SaysNoNotesWereFound()
    {
        File.WriteAllBytes(_wavPath, TestWav.Pcm16(TestSignal.SampleRate, 1, TestSignal.Silence(0.5)));

        (int exitCode, string[] lines) = Run();

        Assert.Equal(0, exitCode);
        Assert.Equal(HarmonizeCommand.NoNotesMessage, lines[^1]);
    }

    [Theory]
    [InlineData(HarmonyStyle.Pop)]
    [InlineData(HarmonyStyle.Modal)]
    [InlineData(HarmonyStyle.Blues)]
    [InlineData(HarmonyStyle.Jazz)]
    [InlineData(HarmonyStyle.Free)]
    public void StyleNotBuiltYet_FailsWithSwedishMessage(HarmonyStyle style)
    {
        AssertFails($"Harmonistilen {HarmonyNames.Style(style)} är inte byggd än.", style: style.ToString());
    }

    [Fact]
    public void AutoKey_FailsUntilKeyDetectionIsBuilt()
    {
        AssertFails(HarmonizeCommand.AutoKeyNotBuiltMessage, key: HarmonySettings.AutoKey);
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
        Assert.Equal([HarmonizeCommand.MissingFileMessage(missing)], lines);
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
        double frequency = Pitch.Frequency(midi * 100.0);
        return [.. Enumerable.Range(0, sampleRate / 2).Select(i => (float)(0.5 * Math.Sin(2 * Math.PI * frequency * i / sampleRate)))];
    }

    private static string[] ChangesWithoutTimes(string[] lines)
    {
        return [.. lines[1..].Select(line => line[(line.IndexOf("s  ", StringComparison.Ordinal) + 3)..])];
    }

    private void AssertFails(string message, string? style = null, string? key = null)
    {
        (int exitCode, string[] lines) = Run(style: style, key: key);

        Assert.Equal(1, exitCode);
        Assert.Equal([message], lines);
    }

    private (int ExitCode, string[] Lines) Run(string? path = null, string? style = null, string? key = null, string? mode = null)
    {
        TestConsole console = TestConsoles.Wide();
        int exitCode = new HarmonizeCommand(console, () => new SettingsLoadResult(TestSettings.Default, null)).Run(path ?? _wavPath, style, key, mode);
        return (exitCode, console.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd()).ToArray());
    }
}
