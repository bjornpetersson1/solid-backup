using SolidBackup.Analysis;
using SolidBackup.Harmony;

namespace SolidBackup.Tests.Harmony;

/// <summary>The Church, Campfire, Drone, Pop and Modal examples in <c>docs/harmony.md</c>.</summary>
public sealed class StyleExampleTests
{
    [Theory]
    [InlineData("K1", "C", Mode.Ionian, "C E G C", "C C C C")]
    [InlineData("K2", "C", Mode.Ionian, "C F G C", "C F G C")]
    [InlineData("K3", "C", Mode.Ionian, "C F# G", "C C C")]
    [InlineData("K4", "C", Mode.Ionian, "F#", "C")]
    [InlineData("K5", "C", Mode.Ionian, "B D E", "G G C")]
    [InlineData("K6", "D", Mode.Dorian, "D B A D", "Dm G Am Dm")]
    [InlineData("K7", "A", Mode.Aeolian, "E B G# A", "Am Em Em Am")]
    public void Church(string example, string tonic, Mode mode, string notes, string chords)
    {
        AssertExample(example, new ChurchRules(), tonic, mode, notes, chords);
    }

    [Theory]
    [InlineData("L1", "C", Mode.Ionian, "E D C", "C G C")]
    [InlineData("L2", "A", Mode.Aeolian, "A G# A G", "Am E Am Am")]
    [InlineData("L3", "C", Mode.Ionian, "C Eb", "C C")]
    public void Campfire(string example, string tonic, Mode mode, string notes, string chords)
    {
        AssertExample(example, new CampfireRules(), tonic, mode, notes, chords);
    }

    [Theory]
    [InlineData(Mode.Ionian)]
    [InlineData(Mode.Phrygian)]
    [InlineData(Mode.Aeolian)]
    public void D1_Drone_IsTheTonicsRootAndFifthInAnyMode(Mode mode)
    {
        AssertExample("D1", new DroneRules(), "D", mode, "D F# C G#", "D5 D5 D5 D5");
    }

    [Theory]
    [InlineData("P1", "C", Mode.Ionian, "C D E B", "C Cadd9 Cadd9 G")]
    [InlineData("P2", "C", Mode.Ionian, "C F A", "C Csus4 Am")]
    [InlineData("P3", "C", Mode.Ionian, "C Bb C", "C Bb C")]
    [InlineData("P4", "C", Mode.Ionian, "C Ab", "C Fm")]
    [InlineData("P5", "A", Mode.Aeolian, "A G#", "Am E")]
    [InlineData("minor chord takes add9", "C", Mode.Ionian, "A B", "Am Amadd9")]
    [InlineData("no sus4 on a minor chord", "C", Mode.Ionian, "A D", "Am G")]
    [InlineData("borrowed bVI", "C", Mode.Ionian, "C Eb", "C Ab")]
    [InlineData("minor set and priority", "A", Mode.Aeolian, "E G", "Am G")]
    [InlineData("minor borrows IV", "A", Mode.Aeolian, "A F#", "Am D")]
    [InlineData("Mixolydian counts as major", "C", Mode.Mixolydian, "C B", "C G")]
    public void Pop(string example, string tonic, Mode mode, string notes, string chords)
    {
        AssertExample(example, new PopRules(), tonic, mode, notes, chords);
    }

    [Theory]
    [InlineData("M1", "D", Mode.Dorian, "D E B C F", "Dm Dm G G Dm")]
    [InlineData("M2", "G", Mode.Mixolydian, "G F D B", "G F G G")]
    [InlineData("M3", "C", Mode.Ionian, "C F E", "C F C")]
    public void Modal(string example, string tonic, Mode mode, string notes, string chords)
    {
        AssertExample(example, new ModalRules(), tonic, mode, notes, chords);
    }

    [Theory]
    [InlineData(Mode.Ionian, "C F E", "C F C")]
    [InlineData(Mode.Dorian, "C A Eb", "Cm F Cm")]
    [InlineData(Mode.Phrygian, "C Db Eb", "Cm Db Cm")]
    [InlineData(Mode.Lydian, "C F# E", "C D C")]
    [InlineData(Mode.Mixolydian, "C Bb E", "C Bb C")]
    [InlineData(Mode.Aeolian, "C Ab Eb", "Cm Ab Cm")]
    [InlineData(Mode.Locrian, "C F# Eb", "Cdim F# Cdim")]
    public void Modal_CharacterNoteGivesTheCharacterChordAndATonicNoteGivesItBack(Mode mode, string notes, string chords)
    {
        AssertExample(mode.ToString(), new ModalRules(), "C", mode, notes, chords);
    }

    [Fact]
    public void PopColour_FollowsTheHoldTimes()
    {
        Harmonizer harmonizer = Melody.Harmonizer(new PopRules(), "C");
        Note[] notes = [new("C4", 0, 500), new("D4", 500, 580), new("C4", 580, 1000), new("D4", 1000, 1500)];

        List<(double TimeMs, Chord Chord)> changes = Melody.Changes(harmonizer, notes);

        Assert.Equal(["C", "Cadd9"], changes.Select(change => change.Chord.Symbol));
        Assert.InRange(changes[1].TimeMs, 1120, 1120 + KeyMelodies.StepMs);
    }

    [Fact]
    public void Modal_DoesNotKeepAChordFromAnotherKey()
    {
        Chord chord = new ModalRules().Choose(9, new Key(7, Mode.Ionian), new Chord(5, ChordQuality.Major));

        Assert.Equal(new Chord(7, ChordQuality.Major), chord);
    }

    [Theory]
    [InlineData(Mode.Ionian, "C Dm Em F G Am")]
    [InlineData(Mode.Dorian, "Cm Dm Eb F Gm Bb")]
    [InlineData(Mode.Phrygian, "Cm Db Eb Fm Ab Bbm")]
    [InlineData(Mode.Lydian, "C D Em G Am Bm")]
    [InlineData(Mode.Mixolydian, "C Dm F Gm Am Bb")]
    [InlineData(Mode.Aeolian, "Cm Eb Fm Gm Ab Bb")]
    [InlineData(Mode.Locrian, "Cdim Db Ebm Fm F# Ab Bbm")]
    public void Church_ChordsMatchTheTablePerMode(Mode mode, string chords)
    {
        IEnumerable<string> actual = new ChurchRules().ChordsIn(new Key(0, mode)).Select(chord => chord.Symbol);

        Assert.Equal(chords.Split(' ').Order(), actual.Order());
    }

    [Theory]
    [InlineData(0, Mode.Ionian, "C F G Am")]
    [InlineData(0, Mode.Mixolydian, "C F G Am")]
    [InlineData(9, Mode.Aeolian, "Am Dm E F")]
    [InlineData(9, Mode.Dorian, "Am Dm E F")]
    public void Campfire_ChordsAreTheMajorOrMinorSet(int tonic, Mode mode, string chords)
    {
        IEnumerable<string> actual = new CampfireRules().ChordsIn(new Key(tonic, mode)).Select(chord => chord.Symbol);

        Assert.Equal(chords.Split(' ').Order(), actual.Order());
    }

    [Fact]
    public void IsBuilt_AgreesWithTryCreate()
    {
        Assert.All(Enum.GetValues<HarmonyStyle>(), style => Assert.Equal(ChordRules.TryCreate(style, out _), ChordRules.IsBuilt(style)));
    }

    [Theory]
    [InlineData(HarmonyStyle.Church, 4)]
    [InlineData(HarmonyStyle.Campfire, 3)]
    [InlineData(HarmonyStyle.Drone, 1)]
    [InlineData(HarmonyStyle.Pop, 5)]
    [InlineData(HarmonyStyle.Modal, 3)]
    [InlineData(HarmonyStyle.Blues, 6)]
    public void PerEstimateCall_AllocatesNothing(HarmonyStyle style, int expectedChanges)
    {
        PitchEstimate[] estimates = Melody.Estimates(Melody.Legato("D", "B", "A", "F#", "-", "-", "-", "-", "-", "E", "C", "G#", "D"));
        Run(Melody.Harmonizer(Rules(style), "D", Mode.Dorian), estimates);

        // A fresh harmonizer and rules, since the estimates' times start again at 0 and Blues keeps state.
        Harmonizer harmonizer = Melody.Harmonizer(Rules(style), "D", Mode.Dorian);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int changes = Run(harmonizer, estimates);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(expectedChanges, changes);
    }

    private static IChordRules Rules(HarmonyStyle style)
    {
        Assert.True(ChordRules.TryCreate(style, out IChordRules? rules));
        return rules;
    }

    private static int Run(Harmonizer harmonizer, PitchEstimate[] estimates)
    {
        int changes = 0;
        foreach (PitchEstimate estimate in estimates)
        {
            if (harmonizer.Process(estimate))
            {
                changes++;
            }
        }

        return changes;
    }

    private static void AssertExample(string example, IChordRules rules, string tonic, Mode mode, string notes, string chords)
    {
        string[] actual = Melody.ChordsAtNoteEnds(Melody.Harmonizer(rules, tonic, mode), Melody.Legato(notes.Split(' ')));

        Assert.True(chords.Split(' ').SequenceEqual(actual), $"{example}: expected {chords}, got {string.Join(' ', actual)}");
    }
}
