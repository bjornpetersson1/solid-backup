using SolidBackup.Accompaniment;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Rhythm;
using SolidBackup.Synthesis;
using SolidBackup.Tests.Synthesis;

namespace SolidBackup.Tests.Accompaniment;

public class RhythmPatternTests
{
    private static readonly Chord C = TestChords.Parse("C");
    private static readonly TempoGrid Grid = new(48_000, 90, 4);

    [Fact]
    public void Grid_PutsEveryStepOnItsExactTimeRounded()
    {
        TempoGrid grid = new(48_000, 70, 4);

        Assert.Equal(41_143, grid.StepSample(1, 1));
        Assert.Equal(20_571, grid.StepSample(1, 2));
        Assert.Equal(288_000, grid.StepSample(7, 1));
        Assert.Equal(3, grid.StepInBar(7, 1));
        Assert.Equal(1, grid.FirstStepAtOrAfter(1, 1));
        Assert.Equal(1, grid.FirstStepAtOrAfter(41_143, 1));
        Assert.Equal(2, grid.FirstStepAtOrAfter(41_144, 1));
    }

    [Fact]
    public void Grid_AllocatesNothing()
    {
        TempoGrid grid = new(48_000, 70, 3);
        long sum = 0;

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (long sample = 0; sample < 1_000_000; sample += 997)
        {
            long step = grid.FirstStepAtOrAfter(sample, 2);
            sum += grid.StepSample(step, 2) + grid.StepInBar(step, 2);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(sum > 0);
    }

    [Fact]
    public void TenMinutesAt70Bpm_EveryBeatIsOnItsExactlyRoundedSample()
    {
        TempoGrid grid = new(48_000, 70, 4);

        // One beat is 288000 / 7 samples; ten minutes hold 700 beats.
        for (long beat = 0; beat < 700; beat++)
        {
            Assert.Equal((long)Math.Round(beat * 288_000 / 7.0, MidpointRounding.AwayFromZero), grid.StepSample(beat, 1));
        }
    }

    [Fact]
    public void TenMinutesAt70Bpm_TheLastBeatIsPlayedOnItsSample()
    {
        const long tenMinutes = 48_000L * 600;
        const long from = tenMinutes - 48_000;

        PatternRun run = PatternRun.Play(new StrumPattern(new TempoGrid(48_000, 70, 4)), 48_000, from, tenMinutes, _ => Frame(ChordShape.Whole));

        // The synthesizer counts from where it started; beat 699 lies at 699 × 288000 / 7 = 28758857.14.
        Assert.Equal(28_758_857, from + run.Starts.Last(tone => tone.Note == 48).Sample);
    }

    [Fact]
    public void StepsIn_YieldsTheStepsOfASpanWithTheirOffsets()
    {
        TempoGrid grid = new(48_000, 90, 4);
        List<TempoGrid.Step> steps = [];

        foreach (TempoGrid.Step step in grid.StepsIn(10_000, 50_000, 2))
        {
            steps.Add(step);
        }

        Assert.Equal([new TempoGrid.Step(1, 6_000), new TempoGrid.Step(2, 22_000), new TempoGrid.Step(3, 38_000)], steps);
    }

    [Theory]
    [InlineData("Arpeggio")]
    [InlineData("Strum")]
    public void Gate_ClosingReleasesTheSoundingTonesAtOnce(string pattern)
    {
        PatternRun run = PatternRun.Play(Create(pattern), 48_000, 60_000, start => start < 40_000 ? C : null);

        Assert.Contains(run.Releases, tone => tone.Sample == 40_000);
        Assert.DoesNotContain(run.Starts, tone => tone.Sample >= 40_000);
    }

    [Theory]
    [InlineData("Arpeggio")]
    [InlineData("Strum")]
    public void Dynamics_SetEachToneWhenItStartsAndTheAccentMultipliesIt(string pattern)
    {
        PatternRun run = PatternRun.Play(Create(pattern), 48_000, 40_000, _ => C, strength: 0.5);

        Assert.All(run.Starts, tone => Assert.Equal(tone.Sample < 1_000 ? 0.5 : 0.5 * Accent.OffBeat, tone.Strength, precision: 9));
    }

    [Fact]
    public void Strum_StoppingRightAfterABeat_LeavesNoStrumToneHanging()
    {
        // The gate closes in the buffer after the beat, while E and G are still waiting to start.
        PatternRun run = PatternRun.Play(new StrumPattern(Grid), 48_000, 40_000, start => start < 32_032 ? C : null);

        foreach (ToneEvent start in run.Starts.Where(tone => tone.Sample >= 32_000))
        {
            Assert.Contains(run.Releases, release => release.Note == start.Note && release.Sample >= start.Sample);
        }
    }

    [Fact]
    public void Arpeggio_AChordPassedThroughBetweenSteps_DoesNotMoveTheVoicing()
    {
        Chord g = TestChords.Parse("G");

        PatternRun plain = PatternRun.Play(new ArpeggioPattern(Grid), 48_000, 40_000, _ => C);
        PatternRun flicker = PatternRun.Play(new ArpeggioPattern(Grid), 48_000, 40_000, start => start is >= 20_000 and < 25_000 ? g : C);

        Assert.Equal(plain.Starts, flicker.Starts);
    }

    [Fact]
    public void ChordCharacter_AppliesFromTheNextStep()
    {
        PatternRun run = PatternRun.Play(new StrumPattern(Grid), 48_000, 40_000, start => Frame(start < 10_000 ? ChordShape.Whole : ChordShape.Full));

        Assert.Equal([48, 52, 55], run.Starts.Where(tone => tone.Sample < 32_000).Select(tone => tone.Note));
        Assert.Equal([36, 48, 52, 55], run.Starts.Where(tone => tone.Sample >= 32_000).Select(tone => tone.Note));
    }

    [Theory]
    [InlineData("Arpeggio")]
    [InlineData("Strum")]
    public void AnOnsetsStrike_IsNotPlayed(string pattern)
    {
        PatternRun plain = PatternRun.Play(Create(pattern), 48_000, 40_000, _ => Frame(ChordShape.Whole));
        PatternRun struck = PatternRun.Play(Create(pattern), 48_000, 40_000, start => Frame(ChordShape.Whole) with { Strike = start == 20_000 });

        Assert.Equal(plain.Starts, struck.Starts);
    }

    [Theory]
    [InlineData("Held")]
    [InlineData("Arpeggio")]
    [InlineData("Strum")]
    public void EveryPattern_ThroughTheChain_AllocatesNothing(string pattern)
    {
        AppSettings settings = Chain.Settings("gate", "onsets", "character", "dynamics") with { RhythmPattern = pattern, Bpm = 240 };
        Accompanist accompanist = new(settings);
        float[] signal = Chain.Join(Chain.Tone(57, -30, 1), Chain.Tone(59, -16, 1));
        float[] output = new float[settings.BufferSize];
        Play(accompanist, signal, output);

        long before = GC.GetAllocatedBytesForCurrentThread();
        Play(accompanist, signal, output);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static AccompanimentFrame Frame(ChordShape shape)
    {
        return AccompanimentFrame.Neutral(C, sounding: true) with { Shape = shape };
    }

    private static IRhythmPattern Create(string pattern)
    {
        return RhythmPatterns.Create(TestSettings.Default with { RhythmPattern = pattern });
    }

    // A plain loop: the test measures this thread's allocations.
    private static void Play(Accompanist accompanist, float[] signal, float[] output)
    {
        for (int start = 0; start + output.Length <= signal.Length; start += output.Length)
        {
            accompanist.Process(signal.AsSpan(start, output.Length), output);
        }
    }
}
