using SolidBackup.Analysis;
using SolidBackup.Harmony;
using SolidBackup.Rhythm;

namespace SolidBackup.Tests.Harmony;

/// <summary>
/// The Jazz examples J1–J10 in docs/harmony.md and the rules around them. Unless a test says otherwise: C major,
/// 120 BPM in 4/4 (a beat is 500 ms, so the loop changes at 0, 1000, 2000 … ms), estimates every millisecond.
/// </summary>
public class JazzTests
{
    [Fact]
    public void J1_TheTonicStartsTheLoopAndV7TakesTheHeldTonicAsItsEleventh()
    {
        AssertChanges(Run("C", [("C", 0, 4000)]), "0 Cmaj7", "1000 Am7", "2000 Dm7", "3000 G7(11)");
    }

    [Fact]
    public void J2_TheLoopWaitsForTheFirstEvenBeatAtLeastABeatLater()
    {
        AssertChanges(Run("C", [(null, 0, 1600), ("C", 1600, 4500)]), "1600 Cmaj7", "3000 Am7", "4000 Dm7");
    }

    [Fact]
    public void J3_AMelodyEndingOnTheTonicStartsTheLoop()
    {
        AssertChanges(Run("C", [("E", 0, 500), ("D", 500, 1000), ("C", 1000, 3500)]), "0 Cmaj7", "620 G7", "1120 Cmaj7", "2000 Am7", "3000 Dm7");
    }

    [Fact]
    public void J4_TheLoopGoesOnThroughSilenceWithAPlainV7()
    {
        AssertChanges(Run("C", [("C", 0, 500), (null, 500, 6500)]), "0 Cmaj7", "1000 Am7", "2000 Dm7", "3000 G7", "4000 Cmaj7", "5000 Am7", "6000 Dm7");
    }

    [Fact]
    public void J5_AForeignNoteGivesASecondaryDominantFollowedByItsTarget()
    {
        AssertChanges(Run("C", [("C", 0, 500), ("F#", 500, 1000), ("G", 1000, 1500)]), "0 Cmaj7", "620 D7", "1120 G7");
    }

    [Fact]
    public void J6_TheSecondForeignNoteModulatesWithIiVI()
    {
        List<Change> changes = Run("C", [("C", 0, 500), ("F#", 500, 1000), ("G", 1000, 1500), ("Bb", 1500, 2000), (null, 2000, 3000)]);

        AssertChanges(changes, "0 Cmaj7", "620 D7", "1120 G7", "1620 Gm7", "2000 C7", "2500 Fmaj7");
        Assert.Equal("F-dur", changes.Single(change => change.Chord == "Gm7").Key);
    }

    [Fact]
    public void J7_APhraseResetReturnsTheKey()
    {
        List<Change> changes = Run("C", [("C", 0, 500), ("F#", 500, 1000), ("G", 1000, 1500), ("Bb", 1500, 2000), (null, 2000, 5000), ("E", 5000, 5500)]);

        AssertChanges(changes, "0 Cmaj7", "620 D7", "1120 G7", "1620 Gm7", "2000 C7", "2500 Fmaj7", "5000 Cmaj7");
        Assert.Equal("C-dur", changes[^1].Key);
    }

    [Fact]
    public void J8_TheMinorLoop()
    {
        AssertChanges(Run("A", [("A", 0, 4000)], Mode.Aeolian), "0 Am7", "1000 Fmaj7", "2000 Bm7b5", "3000 E7(11)");
    }

    [Fact]
    public void J9_In3_4TheLoopChangesOnEveryOtherBeatAcrossTheBarLines()
    {
        AssertChanges(Run("C", [("C", 0, 4000)], beatsPerBar: 3), "0 Cmaj7", "1000 Am7", "2000 Dm7", "3000 G7(11)");
    }

    [Fact]
    public void J10_ANewNoteStopsTheLoopAndKeepsAChordThatContainsIt()
    {
        List<Change> changes = Run("C", [("C", 0, 1100), ("E", 1100, 1600), ("E", 1600, 2600)]);

        AssertChanges(changes, "0 Cmaj7", "1000 Am7");
    }

    [Theory]
    [InlineData("C", "Cmaj7 Dm7 Em7 Fmaj7 G7 Am7 Bm7b5", Mode.Ionian)]
    [InlineData("A", "Am7 Bm7b5 Cmaj7 Dm7 E7 Fmaj7 G7", Mode.Aeolian)]
    public void Chords_AreTheDiatonicSevenths(string tonic, string chords, Mode mode)
    {
        Assert.True(PitchClass.TryParse(tonic, out int root));
        string[] expected = chords.Split(' ');

        // After silence every diatonic note chooses by priority; the chord on each degree is checked through its root note,
        // except the tonic, which starts the loop with the tonic chord.
        Key key = new(root, mode);
        for (int degree = 1; degree <= 7; degree++)
        {
            string note = PitchClass.Name(key.PitchClassOf(degree));
            List<Change> changes = Run(tonic, [(note, 0, 100)], mode);
            Assert.Contains(changes[0].Chord, expected);
        }
    }

    [Theory]
    [InlineData("C#", "A7")]
    [InlineData("Eb", "B7")]
    [InlineData("F#", "D7")]
    [InlineData("Ab", "E7")]
    [InlineData("Bb", "C7")]
    public void EveryChromaticNoteOnCMajor_GivesItsSecondaryDominant(string note, string dominant)
    {
        List<Change> changes = Run("C", [("E", 0, 500), (note, 500, 1000)]);

        Assert.Equal(dominant, changes[^1].Chord);
    }

    [Theory]
    [InlineData("C", Mode.Ionian, "Bb", "F-dur")]
    [InlineData("C", Mode.Ionian, "Ab", "Eb-dur")]
    [InlineData("C", Mode.Ionian, "Db", "D-dur")]
    [InlineData("C", Mode.Ionian, "F#", "G-dur")]
    [InlineData("A", Mode.Aeolian, "Bb", "D-moll")]
    [InlineData("A", Mode.Aeolian, "C#", "B-moll")]
    public void TheSecondForeignNote_MovesToTheNearestKeyWithItTheFlatSideFirst(string tonic, Mode mode, string note, string key)
    {
        string third = PitchClass.Name(new Key(PitchClass.TryParse(tonic, out int root) ? root : 0, mode).PitchClassOf(3));

        List<Change> changes = Run(tonic, [(third, 0, 500), (note, 500, 1000), (third, 1000, 1500), (note, 1500, 2000)], mode);

        Assert.Equal(key, changes.Last().Key);
    }

    [Fact]
    public void AfterTheLoopsV7_TheCommonSuccessorFollowsAndTheEleventhIsDropped()
    {
        List<Change> changes = Run("C", [("C", 0, 3500), ("A", 3500, 4500)]);

        AssertChanges(changes, "0 Cmaj7", "1000 Am7", "2000 Dm7", "3000 G7(11)", "3620 Am7");
    }

    [Fact]
    public void APassingNote_DoesNotTakeTheEleventhAway()
    {
        // The loop's change waits while D might become a new note, then comes as soon as C is back, with the 11th.
        List<Change> changes = Run("C", [("C", 0, 2970), ("D", 2970, 3030), ("C", 3030, 4000)]);

        AssertChanges(changes, "0 Cmaj7", "1000 Am7", "2000 Dm7", "3030 G7(11)");
    }

    [Fact]
    public void TheTonicSungWhileTheLoopRuns_KeepsItsOrder()
    {
        // The phrase resets in the silence (at 2500) while the loop is on Dm7; C comes back at 2600.
        List<Change> changes = Run("C", [("C", 0, 500), (null, 500, 2600), ("C", 2600, 3500)]);

        AssertChanges(changes, "0 Cmaj7", "1000 Am7", "2000 Dm7", "3000 G7(11)");
    }

    [Fact]
    public void APhraseReset_BringsAMovedLoopHome()
    {
        // F becomes the key at 1620 and its cadence ends on Fmaj7 at 2500; F, sung after it, starts F's loop; the silence from 3100 resets the phrase at 5100.
        List<Change> changes = Run("C", [("C", 0, 500), ("F#", 500, 1000), ("G", 1000, 1500), ("Bb", 1500, 2000), (null, 2000, 2600), ("F", 2600, 3100), (null, 3100, 7000)]);

        Change last = changes[^1];
        Assert.True(last.TimeMs > 5100);
        Assert.Equal("C-dur", last.Key);
        Assert.Contains(last.Chord, new[] { "Cmaj7", "Am7", "Dm7", "G7" });
    }

    [Fact]
    public void ALoopChange_WaitsForANewNoteThatIsStillOnHold()
    {
        // E comes at 1950 and is chosen only after its hold time; the loop's change at 2000 must not slip in a Dm7.
        List<Change> changes = Run("C", [("C", 0, 1950), ("E", 1950, 3000)]);

        AssertChanges(changes, "0 Cmaj7", "1000 Am7");
    }

    [Fact]
    public void AnAutoKeyChange_StopsTheLoop()
    {
        JazzRules rules = new(Grid());
        Harmonizer harmonizer = Melody.Harmonizer(rules, "C");
        Feed(harmonizer, [("C", 0, 500), (null, 500, 1500)]);
        Assert.True(rules.KeepsPlaying);

        rules.Reset(new Key(7, Mode.Ionian));

        Assert.False(rules.KeepsPlaying);
    }

    [Fact]
    public void ALoopChange_DoesNotWaitForTheMinimumChordDuration()
    {
        // The tonic chord comes at 900, and the loop's first change at 2000; with a 1500 ms minimum it would wait until 2400.
        List<Change> changes = Run("C", [(null, 0, 900), ("C", 900, 2500)], minChordDurationMs: 1500);

        AssertChanges(changes, "900 Cmaj7", "2000 Am7");
    }

    [Fact]
    public void TheLoop_KeepsThePhraseGoing()
    {
        JazzRules rules = new(Grid());
        Harmonizer harmonizer = Melody.Harmonizer(rules, "C");

        Feed(harmonizer, [("C", 0, 500), (null, 500, 10_000)]);

        Assert.True(harmonizer.InPhrase);
    }

    [Fact]
    public void PerEstimateCall_AllocatesNothing()
    {
        PitchEstimate[] estimates = Melody.Estimates(Notes([("C", 0, 1000), ("F#", 1000, 1500), ("G", 1500, 2000), ("Bb", 2000, 2500), (null, 2500, 6000)]), 48_000, 48);
        Melody.Run(Melody.Harmonizer(new JazzRules(Grid()), "C"), estimates);
        Harmonizer harmonizer = Melody.Harmonizer(new JazzRules(Grid()), "C");

        long before = GC.GetAllocatedBytesForCurrentThread();
        int changes = Melody.Run(harmonizer, estimates);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(changes > 5);
    }

    private static TempoGrid Grid(int beatsPerBar = 4)
    {
        return new TempoGrid(48_000, 120, beatsPerBar);
    }

    private static List<Change> Run(string tonic, (string? Name, double StartMs, double EndMs)[] notes, Mode mode = Mode.Ionian, int beatsPerBar = 4, double minChordDurationMs = 250)
    {
        Harmonizer harmonizer = new(
            new JazzRules(Grid(beatsPerBar)),
            new Key(PitchClass.TryParse(tonic, out int root) ? root : 0, mode),
            new HarmonyTiming(MinNoteForChangeMs: 120, minChordDurationMs, PhraseResetMs: 2000));
        return Feed(harmonizer, notes);
    }

    // One estimate per millisecond, so every time in the examples is met exactly.
    private static List<Change> Feed(Harmonizer harmonizer, (string? Name, double StartMs, double EndMs)[] notes)
    {
        List<Change> changes = [];
        foreach (PitchEstimate estimate in Melody.Estimates(Notes(notes), 48_000, 48))
        {
            if (harmonizer.Process(estimate) && harmonizer.Chord is { } chord && harmonizer.CurrentKey is { } key)
            {
                changes.Add(new Change(estimate.TimeMs, chord.Symbol, SolidBackup.Cli.HarmonyNames.Key(key)));
            }
        }

        return changes;
    }

    private static Note[] Notes((string? Name, double StartMs, double EndMs)[] notes)
    {
        return [.. notes.Select(note => new Note(note.Name is null ? null : $"{note.Name}4", note.StartMs, note.EndMs))];
    }

    private static void AssertChanges(List<Change> changes, params string[] expected)
    {
        Assert.Equal(expected, changes.Select(change => $"{Math.Round(change.TimeMs)} {change.Chord}"));
    }

    private sealed record Change(double TimeMs, string Chord, string Key);
}
