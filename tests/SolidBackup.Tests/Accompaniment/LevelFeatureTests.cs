using SolidBackup.Accompaniment;
using SolidBackup.Analysis;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Synthesis;
using SolidBackup.Tests.Synthesis;

namespace SolidBackup.Tests.Accompaniment;

public class LevelFeatureTests
{
    private const int A3 = 57;
    private const int B3 = 59;

    [Fact]
    public void GateOn_ReleasesEveryToneWhenTheGateCloses()
    {
        AppSettings settings = Chain.Settings("gate") with { Sound = "Organ" };

        ChainStep[] steps = Chain.Run(settings, Chain.Join(Chain.Tone(A3, -20, 1), Analysis.TestSignal.Silence(1)));

        Assert.True(Chain.At(steps, 900).HeldVoices > 0);
        Assert.Equal(0, Chain.At(steps, 1100).HeldVoices);
        Assert.True(Chain.At(steps, 1200).Silent);
    }

    [Fact]
    public void GateOff_KeepsTheChordThroughSilenceUntilThePhraseReset()
    {
        AppSettings settings = Chain.Settings() with { Sound = "Organ" };

        ChainStep[] steps = Chain.Run(settings, Chain.Join(Chain.Tone(A3, -20, 1), Analysis.TestSignal.Silence(3)));

        Assert.All(steps.Where(step => step.Analysis.TimeMs is > 1100 and < 2900), step => Assert.True(step.HeldVoices > 0));
        Assert.True(Chain.At(steps, 3300).Silent);
    }

    [Fact]
    public void GateOn_WhenItOpensAgain_StartsTheChordAgain()
    {
        AppSettings settings = Chain.Settings("gate") with { Sound = "Organ" };

        ChainStep[] steps = Chain.Run(settings, Chain.Join(Chain.Tone(A3, -20, 1), Analysis.TestSignal.Silence(0.5), Chain.Tone(A3, -20, 1)));

        Assert.Equal(0, Chain.At(steps, 1300).HeldVoices);
        Assert.Equal(3, Chain.At(steps, 1800).HeldVoices);
        Assert.Equal(Chain.At(steps, 1300).Frame.Chord, Chain.At(steps, 1800).Frame.Chord);
    }

    [Theory]
    [InlineData(-42, -24)]
    [InlineData(-50, -24)]
    [InlineData(-27, -12)]
    [InlineData(-12, 0)]
    [InlineData(-5, 0)]
    public void Dynamics_MapsTheLevelLinearlyInDb(double levelDbfs, double strengthDb)
    {
        DynamicsFeature dynamics = new(quietDbfs: -42, loudDbfs: -12);

        Assert.Equal(strengthDb, 20 * Math.Log10(dynamics.Strength(levelDbfs)), precision: 6);
    }

    [Fact]
    public void Dynamics_ALouderVoiceGivesALouderChordInTheExpectedRatio()
    {
        AppSettings settings = Chain.Settings("dynamics");

        double soft = Chain.At(Chain.Run(settings, Chain.Tone(A3, -30, 1)), 900).Frame.Strength;
        double loud = Chain.At(Chain.Run(settings, Chain.Tone(A3, -18, 1)), 900).Frame.Strength;

        // −30 and −18 dBFS lie 0.4 and 0.8 of the way from −42 to −12: 24 dB × 0.4 apart.
        Assert.Equal(9.6, 20 * Math.Log10(loud / soft), precision: 1);
    }

    [Fact]
    public void Dynamics_AStepInLevelGivesNoStepInStrength()
    {
        AppSettings settings = Chain.Settings("dynamics");

        ChainStep[] steps = Chain.Run(settings, Chain.Join(Chain.Tone(A3, -40, 1), Chain.Tone(A3, -20, 1)));
        double before = Chain.At(steps, 990).Frame.Strength;
        double after = Chain.At(steps, 1500).Frame.Strength;
        double firstStep = Chain.At(steps, 1010).Frame.Strength;

        Assert.True(after > 4 * before);
        Assert.True(firstStep - before < (after - before) / 5, "The strength jumped with the level.");
    }

    [Theory]
    [InlineData("C", ChordShape.Sparse, new[] { 48, 55 })]
    [InlineData("C", ChordShape.Whole, new[] { 48, 52, 55 })]
    [InlineData("C", ChordShape.Full, new[] { 36, 48, 52, 55 })]
    [InlineData("Cdim", ChordShape.Sparse, new[] { 48, 51 })]
    [InlineData("C5", ChordShape.Sparse, new[] { 48, 55 })]
    public void ChordCharacter_ShapesTheVoicedChord(string symbol, ChordShape shape, int[] expected)
    {
        int[] notes = TestChords.Shaped(TestChords.Parse(symbol), shape);

        Assert.Equal(expected, notes);
    }

    [Theory]
    [InlineData(-36, 2)]
    [InlineData(-24, 3)]
    [InlineData(-14, 4)]
    public void ChordCharacter_TheLevelSetsHowManyTonesSound(double levelDbfs, int tones)
    {
        AppSettings settings = Chain.Settings("character");

        ChainStep[] steps = Chain.Run(settings, Chain.Tone(A3, levelDbfs, 1));

        Assert.Equal(tones, Chain.At(steps, 900).HeldVoices);
    }

    [Fact]
    public void ChordCharacter_ALevelHoveringAtABoundaryDoesNotFlip()
    {
        ChordCharacterFeature character = new(sparseBelowDbfs: -30, fullAboveDbfs: -18);
        List<ChordShape> shapes = [];
        double[] levels = [.. Enumerable.Repeat(-31.0, 10), .. Enumerable.Range(0, 40).Select(i => i % 2 == 0 ? -28.5 : -31.5), .. Enumerable.Repeat(-26.0, 10)];
        foreach (double level in levels)
        {
            AccompanimentFrame frame = AccompanimentFrame.Neutral(null, sounding: true);
            character.AfterHarmony(new AnalysisFrame(0, 0, 4800, level, level, true, false, default), ref frame);
            shapes.Add(frame.Shape);
        }

        Assert.All(shapes[..50], shape => Assert.Equal(ChordShape.Sparse, shape));
        Assert.Equal(ChordShape.Whole, shapes[^1]);
    }

    [Fact]
    public void Onsets_ARepeatedNoteWithAnOnsetIsStruckAnew()
    {
        (int, double)[] notes = [(A3, 1000), (A3, 1000)];

        ChainStep[] with = Chain.Play(Chain.Settings("onsets"), Chain.Notes(Chain.Settings(), -30, notes, 1000));
        ChainStep[] without = Chain.Play(Chain.Settings(), Chain.Notes(Chain.Settings(), -30, notes, 1000));

        ChainStep strike = Assert.Single(with, step => step.Frame.Strike);
        Assert.InRange(strike.Analysis.TimeMs, 1000 + OnsetFeature.NoteWaitMs, 1000 + OnsetFeature.NoteWaitMs + 2);
        Assert.Equal(Chain.At(with, 900).Starts + 3, Chain.At(with, 1500).Starts);
        Assert.Equal(Chain.At(with, 900).Frame.Chord, Chain.At(with, 1500).Frame.Chord);
        Assert.DoesNotContain(without, step => step.Frame.Strike);
        Assert.Equal(Chain.At(without, 900).Starts, Chain.At(without, 1500).Starts);
    }

    [Fact]
    public void Onsets_ANoteChangeWithAnOnsetChangesTheChordBeforeTheHoldTimes()
    {
        // B3 arrives 20 ms after its onset, as the detector needs a moment; Am → G.
        (int, double)[] notes = [(A3, 1020), (B3, 1000)];

        ChainStep[] with = Chain.Play(Chain.Settings("onsets"), Chain.Notes(Chain.Settings(), -30, notes, 1000));
        ChainStep[] without = Chain.Play(Chain.Settings(), Chain.Notes(Chain.Settings(), -30, notes, 1000));

        Assert.InRange(ChangeTime(with), 1020, 1023);
        Assert.InRange(ChangeTime(without), 1020 + TestSettings.Default.MinNoteForChangeMs, 1020 + TestSettings.Default.MinNoteForChangeMs + 3);
        Assert.True(Chain.At(with, 1021.5).Frame.Strike || with.Any(step => step.Frame.Strike && step.Analysis.TimeMs < 1025));
        Assert.Equal("G", Chain.At(with, 1500).Frame.Chord?.Symbol);
    }

    [Fact]
    public void Onsets_ANewNoteAlreadyInTheOnsetsBuffer_IsEvaluatedAtOnce()
    {
        // B3 starts on the onset's own buffer.
        (int, double)[] notes = [(A3, 1000), (B3, 1000)];

        ChainStep[] steps = Chain.Play(Chain.Settings("onsets"), Chain.Notes(Chain.Settings(), -30, notes, 1001));

        Assert.InRange(ChangeTime(steps), 1000, 1003);
        Assert.Contains(steps, step => step.Frame.Strike && step.Analysis.TimeMs < 1003);
    }

    [Fact]
    public void APhraseAfterSilence_StartsAtItsOwnStrengthAndCharacter()
    {
        AppSettings settings = Chain.Settings("gate", "character", "dynamics");

        ChainStep[] steps = Chain.Run(settings, Chain.Join(Analysis.TestSignal.Silence(1), Chain.Tone(A3, -24, 1)));
        ChainStep settled = Chain.At(steps, 1800);
        ChainStep early = steps.First(step => step.Frame.Sounding);

        Assert.Equal(settled.Frame.Shape, early.Frame.Shape);
        Assert.True(early.Frame.Strength > settled.Frame.Strength / 2, $"{early.Frame.Strength} against {settled.Frame.Strength}.");
    }

    [Fact]
    public void Onsets_DoNotResetThePhrase()
    {
        // Blues moves its key on a foreign note and goes back only on a phrase reset (B4 in docs/harmony.md); an onset must not send it back.
        AppSettings settings = Chain.Settings("onsets") with { HarmonyStyle = "Blues" };
        (int, double)[] notes = [(48, 600), (56, 600), (56, 600)];

        ChainStep[] steps = Chain.Play(settings, Chain.Notes(settings, -30, notes, 1200));

        Assert.Contains(steps, step => step.Frame.Strike && step.Analysis.TimeMs > 1200);
        Assert.Equal("Bb7", Chain.At(steps, 1700).Frame.Chord?.Symbol);
    }

    private static double ChangeTime(ChainStep[] steps)
    {
        Chord? first = Chain.At(steps, 900).Frame.Chord;
        return steps.First(step => step.Analysis.TimeMs > 1000 && step.Frame.Chord != first).Analysis.TimeMs;
    }
}
