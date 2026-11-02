using SolidBackup.Accompaniment;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Rhythm;
using SolidBackup.Tests.Synthesis;

namespace SolidBackup.Tests.Accompaniment;

/// <summary>
/// The examples R1–R12 in docs/rhythm.md. Unless a test says otherwise: 48 kHz, 90 BPM (a beat is 32000 samples, an
/// eighth 16000), 4/4, the chord C voiced C3 E3 G3 from sample 0. Positions are exact; the criterion allows 1 ms.
/// </summary>
public class RhythmExampleTests
{
    private const int C3 = 48;
    private const int E3 = 52;
    private const int G3 = 55;
    private const int Bb3 = 58;
    private const int A3 = 57;

    private static readonly Chord C = TestChords.Parse("C");

    [Fact]
    public void R1_StrumStrikesEveryBeatTenMsApartWithTheDownbeatAccent()
    {
        PatternRun run = PatternRun.Play(new StrumPattern(Grid()), 48_000, 160_000, _ => C);

        long[] beats = [0, 32_000, 64_000, 96_000, 128_000];
        Assert.Equal(beats.SelectMany(beat => new[] { (beat, C3), (beat + 480, E3), (beat + 960, G3) }), run.Starts.Select(tone => (tone.Sample, tone.Note)));
        Assert.All(run.Starts.Where(tone => tone.Sample % 128_000 < 960), tone => Assert.Equal(1, tone.Strength));
        Assert.All(run.Starts.Where(tone => tone.Sample % 128_000 >= 32_000), tone => Assert.Equal(Accent.OffBeat, tone.Strength));
    }

    [Fact]
    public void R2_ArpeggioPlaysTheStepInBarModTheTones()
    {
        PatternRun run = PatternRun.Play(new ArpeggioPattern(Grid()), 48_000, 144_000, _ => C);

        AssertStarts(run, (0, C3), (16_000, E3), (32_000, G3), (48_000, C3), (64_000, E3), (80_000, G3), (96_000, C3), (112_000, E3), (128_000, C3));
    }

    [Fact]
    public void R3_ArpeggioIn3_4HasSixEighthsABar()
    {
        PatternRun run = PatternRun.Play(new ArpeggioPattern(Grid(beatsPerBar: 3)), 48_000, 112_000, _ => C);

        AssertStarts(run, (0, C3), (16_000, E3), (32_000, G3), (48_000, C3), (64_000, E3), (80_000, G3), (96_000, C3));
    }

    [Fact]
    public void R4_ArpeggioOfAFourToneChord()
    {
        PatternRun run = PatternRun.Play(new ArpeggioPattern(Grid()), 48_000, 128_000, _ => TestChords.Parse("C7"));

        AssertStarts(run, (0, C3), (16_000, E3), (32_000, G3), (48_000, Bb3), (64_000, C3), (80_000, E3), (96_000, G3), (112_000, Bb3));
    }

    [Fact]
    public void R5_ArpeggioTakesANewChordFromTheNextStep()
    {
        Chord f = TestChords.Parse("F");

        PatternRun run = PatternRun.Play(new ArpeggioPattern(Grid()), 48_000, 64_000, start => start < 20_000 ? C : f);

        AssertStarts(run, (0, C3), (16_000, E3), (32_000, A3), (48_000, C3));
        Assert.Contains(run.Releases, tone => tone is { Sample: 32_000, Note: E3 });
        Assert.DoesNotContain(run.Releases, tone => tone.Sample is > 16_000 and < 32_000);
    }

    [Fact]
    public void R6_HeldChangesAtOnce()
    {
        Chord f = TestChords.Parse("F");

        PatternRun run = PatternRun.Play(new HeldPattern(), 48_000, 40_000, start => start < 20_000 ? C : f);

        Assert.Contains(run.Starts, tone => tone is { Sample: 20_000, Note: 53 });
        Assert.Contains(run.Releases, tone => tone is { Sample: 20_000, Note: E3 });
    }

    [Fact]
    public void R7_TheFirstChordAfterSilenceWaitsForTheNextBeat()
    {
        PatternRun run = PatternRun.Play(new StrumPattern(Grid()), 48_000, 70_000, start => start < 40_000 ? null : C);

        Assert.Equal(64_000, run.Starts[0].Sample);
    }

    [Fact]
    public void R8_AChordOnAStepsSamplePlaysOnThatStep()
    {
        PatternRun run = PatternRun.Play(new StrumPattern(Grid()), 48_000, 70_000, start => start < 64_000 ? null : C);

        Assert.Equal(64_000, run.Starts[0].Sample);
    }

    [Fact]
    public void R9_At70BpmEachBeatIsRoundedFromItsExactTime()
    {
        PatternRun run = PatternRun.Play(new StrumPattern(Grid(bpm: 70)), 48_000, 290_000, _ => C);

        Assert.Equal([0, 41_143, 82_286, 123_429, 164_571, 205_714, 246_857, 288_000], run.Starts.Where(tone => tone.Note == C3).Select(tone => tone.Sample));
    }

    [Fact]
    public void R10_At44_1KhzTheSpreadIs441Samples()
    {
        PatternRun run = PatternRun.Play(new StrumPattern(new TempoGrid(44_100, 120, 4)), 44_100, 66_000, _ => C);

        Assert.Equal([(0L, C3), (441L, E3), (882L, G3), (22_050L, C3), (22_491L, E3), (22_932L, G3), (44_100L, C3), (44_541L, E3), (44_982L, G3)], run.Starts.Select(tone => (tone.Sample, tone.Note)));
    }

    [Fact]
    public void R11_ThePatternStopsAfterPhraseResetMsOfSilence()
    {
        ChainStep[] steps = ThroughTheChain(out PatternRun run, [(60, 100_000), (-1, 250_000)]);

        Assert.Contains(run.Starts, tone => tone.Sample == 192_000);
        Assert.DoesNotContain(run.Starts, tone => tone.Sample >= 196_000);
        Assert.NotEmpty(steps);
    }

    [Fact]
    public void R12_TheGridKeepsRunningThroughAStop()
    {
        ThroughTheChain(out PatternRun run, [(60, 100_000), (-1, 200_000), (60, 50_000)]);

        Assert.DoesNotContain(run.Starts, tone => tone.Sample is >= 196_000 and < 304_000);
        ToneEvent first = run.Starts.First(tone => tone.Sample >= 300_000);
        Assert.Equal((304_000L, C3), (first.Sample, first.Note));
    }

    private static TempoGrid Grid(int bpm = 90, int beatsPerBar = 4)
    {
        return new TempoGrid(48_000, bpm, beatsPerBar);
    }

    private static void AssertStarts(PatternRun run, params (long Sample, int Note)[] expected)
    {
        Assert.Equal(expected, run.Starts.Select(tone => (tone.Sample, tone.Note)));
    }

    // The arpeggio through the whole chain with the gate off, so the stop comes from PhraseResetMs; samples as (note or −1, length).
    private static ChainStep[] ThroughTheChain(out PatternRun run, (int Midi, long Samples)[] parts)
    {
        AppSettings settings = Chain.Settings() with { RhythmPattern = "Arpeggio", BufferSize = PatternRun.BufferSize };
        Accompanist accompanist = new(settings);
        run = new PatternRun();
        accompanist.Synthesizer.Listener = run;
        return Chain.PlayWith(accompanist, Chain.Notes(settings, -30, [.. parts.Select(part => (part.Midi, part.Samples * 1000.0 / settings.SampleRate))]));
    }
}
