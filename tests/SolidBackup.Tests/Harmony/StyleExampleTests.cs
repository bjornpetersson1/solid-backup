using SolidBackup.Analysis;
using SolidBackup.Harmony;

namespace SolidBackup.Tests.Harmony;

/// <summary>The Church, Campfire and Drone examples in <c>docs/harmony.md</c>.</summary>
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
    public void PerEstimateCall_AllocatesNothing(HarmonyStyle style, int expectedChanges)
    {
        Assert.True(ChordRules.TryCreate(style, out IChordRules? rules));
        PitchEstimate[] estimates = Melody.Estimates(Melody.Legato("D", "B", "A", "F#", "-", "-", "-", "-", "-", "E", "C", "G#", "D"));
        Run(Melody.Harmonizer(rules, "D", Mode.Dorian), estimates);

        // A fresh harmonizer, since the estimates' times start again at 0.
        Harmonizer harmonizer = Melody.Harmonizer(rules, "D", Mode.Dorian);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int changes = Run(harmonizer, estimates);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(expectedChanges, changes);
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
