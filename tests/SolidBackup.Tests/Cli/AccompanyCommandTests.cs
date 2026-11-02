using SolidBackup.Audio;
using SolidBackup.Cli;
using SolidBackup.Tests.Accompaniment;
using SolidBackup.Tests.Analysis;
using SolidBackup.Tests.Audio;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public sealed class AccompanyCommandTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "solid-backup-tests", Guid.NewGuid().ToString("N"));
    private readonly float[] _voice = Chain.Join(TestSignal.Silence(0.2), Chain.Tone(57, -20, 1), TestSignal.Silence(0.3));
    private readonly string _input;

    public AccompanyCommandTests()
    {
        Directory.CreateDirectory(_folder);
        _input = Path.Combine(_folder, "voice.wav");
        File.WriteAllBytes(_input, TestWav.Float32(TestSignal.SampleRate, 1, _voice));
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Run_WritesTheVoiceMixedWithTheAccompaniment()
    {
        TestConsole console = TestConsoles.Wide();
        string output = Path.Combine(_folder, "out.wav");

        int exitCode = Command(console).Run(_input, output, new AccompanyOptions(Key: "C"));

        Assert.Equal(0, exitCode);
        float[] mixed = WavReader.Read(output).Samples;
        Assert.Equal(_voice.Length, mixed.Length);
        Assert.Contains(mixed.Select((sample, i) => sample - _voice[i]), difference => Math.Abs(difference) > 0.01);
        Assert.Contains("nivåfunktioner: gate, onsets, character, dynamics", console.Output);
    }

    [Fact]
    public void Run_NoVoice_WritesTheAccompanimentAlone()
    {
        string output = Path.Combine(_folder, "out.wav");

        Command(TestConsoles.Wide()).Run(_input, output, new AccompanyOptions(Key: "C", NoVoice: true));

        float[] accompaniment = WavReader.Read(output).Samples;
        Assert.All(accompaniment[..(TestSignal.SampleRate / 10)], sample => Assert.Equal(0, sample));
        Assert.Contains(accompaniment, sample => Math.Abs(sample) > 0.01);
    }

    [Theory]
    [InlineData("none", "nivåfunktioner: none")]
    [InlineData("dynamics,gate", "nivåfunktioner: gate, dynamics")]
    public void Run_Features_OverrideTheSwitches(string features, string shown)
    {
        TestConsole console = TestConsoles.Wide();

        Command(console).Run(_input, Path.Combine(_folder, "out.wav"), new AccompanyOptions(Key: "C", Features: features));

        Assert.Contains(shown, console.Output);
    }

    [Theory]
    [InlineData("gate,loud")]
    [InlineData("gate,,dynamics")]
    [InlineData("")]
    public void Run_UnknownFeature_FailsWithSwedishMessage(string features)
    {
        TestConsole console = TestConsoles.Wide();

        int exitCode = Command(console).Run(_input, Path.Combine(_folder, "out.wav"), new AccompanyOptions(Features: features));

        Assert.Equal(1, exitCode);
        Assert.Contains(AccompanyCommand.UnknownFeatureMessage(features), console.Output);
    }

    [Fact]
    public void Run_LoudVoice_IsTurnedDownRatherThanClipped()
    {
        string loud = Path.Combine(_folder, "loud.wav");
        File.WriteAllBytes(loud, TestWav.Float32(TestSignal.SampleRate, 1, TestSignal.Sine(57, 0.95, 1)));
        string output = Path.Combine(_folder, "out.wav");

        Command(TestConsoles.Wide()).Run(loud, output, new AccompanyOptions(Key: "C"));

        float[] mixed = WavReader.Read(output).Samples;
        Assert.True(mixed.Max(Math.Abs) <= 1);
        Assert.True(mixed.Count(sample => Math.Abs(sample) >= 0.9999f) < 3, "The mix was clipped.");
    }

    [Fact]
    public void Run_KeyWithAStyleWithoutKey_Warns()
    {
        TestConsole console = TestConsoles.Wide();

        Command(console).Run(_input, Path.Combine(_folder, "out.wav"), new AccompanyOptions(Style: "Free", Key: "G"));

        Assert.Contains(HarmonizeCommand.KeyIgnoredMessage, console.Output);
    }

    [Fact]
    public void Run_WithAPattern_ChangesTheAccompaniment()
    {
        string held = Path.Combine(_folder, "held.wav");
        string strum = Path.Combine(_folder, "strum.wav");

        Command(TestConsoles.Wide()).Run(_input, held, new AccompanyOptions(Key: "C", NoVoice: true));
        Command(TestConsoles.Wide()).Run(_input, strum, new AccompanyOptions(Key: "C", NoVoice: true, Rhythm: new RhythmOverrides("Strum", 120)));

        Assert.NotEqual(WavReader.Read(held).Samples, WavReader.Read(strum).Samples);
    }

    [Fact]
    public void Run_MissingRecording_FailsWithSwedishMessage()
    {
        TestConsole console = TestConsoles.Wide();
        string missing = Path.Combine(_folder, "saknas.wav");

        int exitCode = Command(console).Run(missing, Path.Combine(_folder, "out.wav"), new AccompanyOptions());

        Assert.Equal(1, exitCode);
        Assert.Contains(HarmonizeCommand.MissingFileMessage(missing), console.Output);
    }

    [Fact]
    public void Run_StyleNotBuilt_FailsWithSwedishMessage()
    {
        TestConsole console = TestConsoles.Wide();

        int exitCode = Command(console).Run(_input, Path.Combine(_folder, "out.wav"), new AccompanyOptions(Style: "Jazz"));

        Assert.Equal(1, exitCode);
        Assert.Contains(HarmonizeCommand.NotBuiltMessage("Jazz"), console.Output);
    }

    private static AccompanyCommand Command(TestConsole console)
    {
        return new AccompanyCommand(console, () => new(TestSettings.Default, null));
    }
}
