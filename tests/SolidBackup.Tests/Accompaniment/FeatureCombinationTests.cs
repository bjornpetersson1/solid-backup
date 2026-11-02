using SolidBackup.Accompaniment;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Measurement;
using SolidBackup.Synthesis;
using SolidBackup.Tests.Synthesis;

namespace SolidBackup.Tests.Accompaniment;

/// <summary>
/// Every combination of the four level-feature switches on the singing recording, each checked against the rules
/// in specs/E5-2-level-features.md. The recording is analysed once; each combination plays that analysis.
/// </summary>
public class FeatureCombinationTests(FeatureCombinationTests.SingingRuns runs) : IClassFixture<FeatureCombinationTests.SingingRuns>
{
    private const double SettleMs = 100;

    private static readonly string[] Names = ["gate", "onsets", "character", "dynamics"];

    private static readonly CorpusClip Singing = RecordedCorpus.Load(CorpusDirectory.Locate(), "singing");

    public static TheoryData<int> Combinations => [.. Enumerable.Range(0, 16)];

    public static TheoryData<int, int> SingleSwitchPairs
    {
        get
        {
            TheoryData<int, int> pairs = [];
            foreach (int switches in Enumerable.Range(0, 16))
            {
                foreach (int bit in Enumerable.Range(0, 4).Where(bit => (switches & (1 << bit)) == 0))
                {
                    pairs.Add(switches, switches | (1 << bit));
                }
            }

            return pairs;
        }
    }

    private static IEnumerable<ReferenceSegment> Segments => Singing.Reference.Segments;

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Gate_SilenceIsSilentWithItAndKeepsTheChordWithoutIt(int switches)
    {
        ChainStep[] steps = runs.Steps[switches];
        AppSettings settings = Settings(switches);
        double firstChordMs = steps.First(step => step.Frame.Sounding).Analysis.TimeMs;
        foreach (ReferenceSegment silence in Segments.Where(segment => segment.Kind is SegmentKind.Silence or SegmentKind.Breath))
        {
            if (settings.GateOn)
            {
                double quietFrom = silence.StartMs + settings.GateHoldMs + SoundPreset.For(Sound.Pad).PlayedReleaseMs + SettleMs;
                Assert.All(Within(steps, quietFrom, silence.EndMs), step => Assert.True(step.Silent, $"Sound at {step.Analysis.TimeMs:0} ms."));
            }
            else if (silence.StartMs > firstChordMs && silence.EndMs - silence.StartMs < settings.PhraseResetMs)
            {
                Assert.All(Within(steps, silence.StartMs, silence.EndMs), step => Assert.True(step.HeldVoices > 0, $"No chord at {step.Analysis.TimeMs:0} ms."));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Dynamics_StrengthRanksWithTheLevelWithItAndIsFullWithoutIt(int switches)
    {
        ChainStep[] steps = runs.Steps[switches];
        if (!Settings(switches).DynamicsOn)
        {
            Assert.All(steps, step => Assert.Equal(1, step.Frame.Strength));
            return;
        }

        List<(double Level, double Strength)> notes = [];
        foreach (ReferenceSegment note in Segments.Where(segment => segment.IsVoiced))
        {
            ArraySegment<ChainStep> within = Within(steps, note.StartMs + SettleMs, note.EndMs);
            notes.Add((within.Average(step => step.Analysis.LevelDbfs), within.Average(step => step.Frame.Strength)));
        }

        Assert.True(Spearman(notes) >= 0.8, $"Spearman {Spearman(notes):0.00}");
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void ChordCharacter_TheSoundingTonesFollowTheShapeWithItAndTheWholeChordWithoutIt(int switches)
    {
        ChainStep[] steps = runs.Steps[switches];
        bool characterOn = Settings(switches).ChordCharacterOn;
        foreach (ReferenceSegment note in Segments.Where(segment => segment.IsVoiced))
        {
            foreach (ChainStep step in Within(steps, note.StartMs + SettleMs, note.EndMs).Where(step => step.Frame is { Sounding: true, Chord: not null }))
            {
                Assert.True(characterOn || step.Frame.Shape == ChordShape.Whole);
                Assert.Equal(TestChords.Shaped(step.Frame.Chord!.Value, step.Frame.Shape).Length, step.HeldVoices);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Onsets_EveryOnsetIsStruckWithinTheWaitWithThemAndNothingIsStruckWithout(int switches)
    {
        ChainStep[] steps = runs.Steps[switches];
        if (!Settings(switches).OnsetsOn)
        {
            Assert.DoesNotContain(steps, step => step.Frame.Strike);
            return;
        }

        double bufferMs = Settings(switches).BufferSize * 1000.0 / Settings(switches).SampleRate;
        foreach (ChainStep onset in steps.Where(step => step.Analysis.Onset))
        {
            double at = onset.Analysis.TimeMs;
            Assert.Contains(steps, step => step.Frame.Strike && step.Analysis.TimeMs > at && step.Analysis.TimeMs <= at + OnsetFeature.NoteWaitMs + bufferMs);
        }
    }

    [Theory]
    [MemberData(nameof(SingleSwitchPairs))]
    public void SwitchingOneFeature_ChangesOnlyItsOwnAspect(int off, int on)
    {
        ChainStep[] a = runs.Steps[off];
        ChainStep[] b = runs.Steps[on];
        string feature = Names[int.Log2(on ^ off)];
        if (feature == "gate")
        {
            // The gate changes which notes reach the harmony; the level-driven aspects must still agree over the notes.
            foreach (ReferenceSegment note in Segments.Where(segment => segment.IsVoiced))
            {
                AssertSame(Within(a, note.StartMs, note.EndMs), Within(b, note.StartMs, note.EndMs), frame => frame with { Chord = null, Sounding = false, Strike = false });
            }

            return;
        }

        // Each feature's own aspect is blanked out; everything else must match buffer for buffer. Onsets own the chord's timing too.
        AssertSame(a, b, feature switch
        {
            "onsets" => frame => frame with { Chord = null, Strike = false },
            "character" => frame => frame with { Shape = ChordShape.Whole },
            _ => frame => frame with { Strength = 1 },
        });
    }

    private static AppSettings Settings(int switches)
    {
        return Chain.Settings([.. Names.Where((_, bit) => (switches & (1 << bit)) != 0)]) with { Key = "auto" };
    }

    // The steps are in time order, so the range is found by binary search.
    private static ArraySegment<ChainStep> Within(ChainStep[] steps, double fromMs, double toMs)
    {
        int from = FirstAtOrAfter(steps, fromMs);
        return new ArraySegment<ChainStep>(steps, from, Math.Max(0, FirstAtOrAfter(steps, toMs) - from));
    }

    private static int FirstAtOrAfter(ChainStep[] steps, double timeMs)
    {
        int low = 0;
        int high = steps.Length;
        while (low < high)
        {
            int middle = (low + high) / 2;
            (low, high) = steps[middle].Analysis.TimeMs < timeMs ? (middle + 1, high) : (low, middle);
        }

        return low;
    }

    private static void AssertSame(IReadOnlyList<ChainStep> a, IReadOnlyList<ChainStep> b, Func<AccompanimentFrame, AccompanimentFrame> others)
    {
        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            if (others(a[i].Frame) != others(b[i].Frame))
            {
                Assert.Fail($"At {a[i].Analysis.TimeMs:0} ms: {a[i].Frame} against {b[i].Frame}.");
            }
        }
    }

    private static double Spearman(List<(double Level, double Strength)> pairs)
    {
        double[] levels = Ranks(pairs.Select(pair => pair.Level).ToArray());
        double[] strengths = Ranks(pairs.Select(pair => pair.Strength).ToArray());
        double meanLevel = levels.Average();
        double meanStrength = strengths.Average();
        double covariance = levels.Zip(strengths, (l, s) => (l - meanLevel) * (s - meanStrength)).Sum();
        double spread = Math.Sqrt(levels.Sum(l => (l - meanLevel) * (l - meanLevel)) * strengths.Sum(s => (s - meanStrength) * (s - meanStrength)));
        return covariance / spread;
    }

    // Average ranks, so ties share their rank.
    private static double[] Ranks(double[] values)
    {
        return [.. values.Select(value => values.Count(other => other < value) + ((values.Count(other => other == value) + 1) / 2.0))];
    }

    /// <summary>The singing recording analysed once and played with each of the 16 combinations, for this class's tests only.</summary>
    public sealed class SingingRuns
    {
        public SingingRuns()
        {
            AnalysisFrame[] analyses = Chain.Analyze(Settings(0b1111), Singing.Samples);
            Steps = [.. Enumerable.Range(0, 16).Select(switches => Chain.Play(Settings(switches), analyses))];
        }

        public ChainStep[][] Steps { get; }
    }
}
