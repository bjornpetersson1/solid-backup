using SolidBackup.Analysis;
using SolidBackup.Harmony;
using SolidBackup.Measurement;

namespace SolidBackup.Tests.Harmony;

/// <summary>Free's three-tone chords and its root rule (E7-4, <c>docs/harmony.md</c>).</summary>
public sealed class FreeTests
{
    // docs/harmony.md's table: each shape's tones above the root and its symbol on C, in order.
    private static readonly (int[] Tones, string OnC)[] Table =
    [
        ([0, 4, 7], "C"),
        ([0, 3, 7], "Cm"),
        ([0, 5, 7], "Csus4"),
        ([0, 4, 10], "C7(no5)"),
        ([0, 3, 10], "Cm7(no5)"),
        ([0, 4, 11], "Cmaj7(no5)"),
        ([0, 2, 4], "Cadd9(no5)"),
        ([0, 2, 3], "Cmadd9(no5)"),
        ([0, 3, 6], "Cdim"),
        ([0, 4, 8], "Caug"),
        ([0, 7, 10], "C7(no3)"),
        ([0, 7, 11], "Cmaj7(no3)"),
        ([0, 4, 6], "C(#11,no5)"),
        ([0, 3, 4], "C(#9,no5)"),
        ([0, 3, 11], "Cm(maj7,no5)"),
        ([0, 1, 3], "Cm(b9,no5)"),
        ([0, 1, 7], "C5(b9)"),
        ([0, 6, 7], "C5(#11)"),
        ([0, 1, 2], "C(b9,9)"),
    ];

    [Fact]
    public void TheShapes_AreTheTablesAndCoverEverySetOfThreePitchClasses()
    {
        Assert.Equal(Table.Select(row => row.OnC), FreeRules.Shapes.Select(shape => new Chord(0, shape).Symbol));
        Assert.Equal(Table.Select(row => row.Tones), FreeRules.Shapes.Select(shape => Chord.Intervals(shape).ToArray()));
        HashSet<int> sets = [.. FreeRules.Shapes.SelectMany(shape => Enumerable.Range(0, 12).Select(root => new Chord(root, shape).PitchClassMask))];
        Assert.Equal(220, sets.Count);
    }

    [Fact]
    public void EveryNoteAfterEveryChord_GivesThreePitchClassesWithTheNote_NamedByTheTable()
    {
        foreach (Chord? current in AllChords().Select(chord => (Chord?)chord).Append(null))
        {
            for (int note = 0; note < 12; note++)
            {
                Chord chord = new FreeRules().Choose(note, default, current);

                Assert.Equal(3, chord.PitchClasses.Count);
                Assert.True(chord.Contains(note), $"{chord} after {current} does not contain {PitchClass.Name(note)}.");
                // A kept chord keeps its name; a new augmented triad is rooted on the note.
                Assert.Equal(chord == current ? current : Named(chord.PitchClassMask, note), chord);
            }
        }
    }

    [Fact]
    public void OverRandomNotes_NoTwoChangesInARowKeepTheRoot()
    {
        // 10 000 notes of 150–700 ms, one in four up to 3 s, one in eight a silence; one estimate per 64-sample buffer.
        Random random = new(7);
        List<PitchEstimate> estimates = [];
        double time = 0;
        for (int i = 0; i < 10_000; i++)
        {
            double end = time + (random.Next(4) == 0 ? random.Next(50, 3000) : random.Next(150, 700));
            int? midi = random.Next(8) == 0 ? null : 60 + random.Next(12);
            for (; time < end; time += KeyMelodies.StepMs)
            {
                estimates.Add(midi is { } note ? new PitchEstimate(time, true, note * 100.0, 1) : PitchEstimate.NoNote(time, 0));
            }
        }

        AssertNoTwoChangesInARowKeepTheRoot([.. estimates]);
    }

    [Fact]
    public void OverRandomNoteSequences_TheRulesNeverKeepTheRootTwiceInARow()
    {
        // The rules alone, on 100 000 random notes, with a phrase reset now and then.
        Random random = new(11);
        FreeRules rules = new();
        RootRule rule = new();
        Chord? current = null;
        for (int i = 0; i < 100_000; i++)
        {
            if (random.Next(50) == 0)
            {
                rules.Reset(default);
                current = null;
            }

            Chord chord = rules.Choose(random.Next(12), default, current);
            if (chord != current)
            {
                rule.Change(current, chord);
            }

            current = chord;
        }
    }

    [Fact]
    public void OverEveryCorpusMelody_EveryChordHasThreeTonesWithTheNote_AndTheRootMoves()
    {
        foreach (CorpusClip clip in KeyMelodies.Clips)
        {
            foreach (PitchEstimate[] estimates in new[] { KeyMelodies.Estimates(clip), KeyMelodies.Legato(clip) })
            {
                // A change is made for the note sounding then, so it contains it.
                AssertNoTwoChangesInARowKeepTheRoot(estimates, (estimate, chord) =>
                {
                    Assert.Equal(3, chord.PitchClasses.Count);
                    Assert.True(chord.Contains(estimate.MidiNote % 12), $"{clip.Name}: {chord} at {estimate.TimeMs:0} ms.");
                });
            }
        }
    }

    [Fact]
    public void ACurrentChordItDidNotChoose_StartsTheRootRuleAfresh()
    {
        FreeRules rules = new();
        Chord c = rules.Choose(0, default, null);
        Chord sus4 = rules.Choose(5, default, c);
        Assert.Equal("Csus4", sus4.Symbol);

        // After C → Csus4 the next change must leave C. From a C7(no5) these rules did not choose, it need not: D gives
        // Cadd9(no5), as from fresh rules, not Bbadd9(no5).
        Chord fromOther = rules.Choose(2, default, new Chord(0, ChordQuality.Dominant7No5));

        Assert.Equal(new FreeRules().Choose(2, default, new Chord(0, ChordQuality.Dominant7No5)), fromOther);
    }

    [Fact]
    public void EverySymbol_IsReadBackAsItsChord_OnEveryRoot()
    {
        foreach (Chord chord in AllChords())
        {
            Assert.True(ChordSymbol.TryParse(chord.Symbol, out Chord read));
            Assert.Equal(chord, read);
        }
    }

    // Changes within a phrase: a phrase reset (a silence of PhraseResetMs) starts the rule afresh.
    private static void AssertNoTwoChangesInARowKeepTheRoot(PitchEstimate[] estimates, Action<PitchEstimate, Chord>? onChange = null)
    {
        Harmonizer harmonizer = Harmonizer.FromSettings(TestSettings.Examples with { HarmonyStyle = "Free" });
        RootRule rule = new();
        double silenceSince = double.NegativeInfinity;
        Chord? before = null;
        foreach (PitchEstimate estimate in estimates)
        {
            if (!estimate.HasNote && double.IsNegativeInfinity(silenceSince))
            {
                silenceSince = estimate.TimeMs;
            }

            if (estimate.HasNote)
            {
                before = estimate.TimeMs - silenceSince >= TestSettings.Examples.PhraseResetMs ? null : before;
                silenceSince = double.NegativeInfinity;
            }

            if (harmonizer.Process(estimate) && harmonizer.Chord is { } chord)
            {
                onChange?.Invoke(estimate, chord);
                rule.Change(before, chord);
                before = chord;
            }
        }
    }

    private static IEnumerable<Chord> AllChords()
    {
        return FreeRules.Shapes.SelectMany(shape => Enumerable.Range(0, 12).Select(root => new Chord(root, shape)));
    }

    // The table's name for a set: the first shape that matches, on its root; the augmented triad on the note.
    private static Chord Named(int mask, int augmentedRoot)
    {
        for (int shape = 0; shape < Table.Length; shape++)
        {
            for (int root = 0; root < 12; root++)
            {
                Chord chord = new(root, FreeRules.Shapes[shape]);
                if (chord.PitchClassMask == mask && (chord.Quality != ChordQuality.Augmented || root == augmentedRoot))
                {
                    return chord;
                }
            }
        }

        throw new InvalidOperationException("No shape matches.");
    }

    // The rule itself: a change after one that kept the root must move it; a change from no chord starts afresh.
    private sealed class RootRule
    {
        private bool _lastKept;

        public void Change(Chord? previous, Chord chord)
        {
            bool kept = previous is { } before && chord.Root == before.Root;
            Assert.False(kept && _lastKept, $"{previous} → {chord} kept the root twice in a row.");
            _lastKept = kept;
        }
    }
}
