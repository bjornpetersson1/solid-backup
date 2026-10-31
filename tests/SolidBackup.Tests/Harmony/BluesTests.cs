using SolidBackup.Analysis;
using SolidBackup.Configuration;
using SolidBackup.Harmony;

namespace SolidBackup.Tests.Harmony;

/// <summary>The Blues examples in <c>docs/harmony.md</c>, and the current key the engine reports.</summary>
public sealed class BluesTests
{
    private static readonly Key C = new(0, Mode.Ionian);

    [Theory]
    [InlineData("B1", "C E F A", "C7 C7 F7 F7", "C C C C")]
    [InlineData("B2", "C Eb Gb Bb", "C7 C7 C7 C7", "C C C C")]
    [InlineData("B3", "C B C", "C7 G7 C7", "C C C")]
    [InlineData("B4", "C Ab", "C7 Bb7", "C F")]
    [InlineData("B5", "C Db", "C7 Eb7", "C Bb")]
    public void Example(string example, string notes, string chords, string keys)
    {
        (string[] actualChords, string[] actualKeys) = Run(Melody.Harmonizer(new BluesRules(), "C"), Melody.Legato(notes.Split(' ')));

        Assert.True(chords.Split(' ').SequenceEqual(actualChords), $"{example}: expected {chords}, got {string.Join(' ', actualChords)}");
        Assert.Equal(keys.Split(' '), actualKeys);
    }

    [Fact]
    public void B6_PhraseReset_ReturnsToTheKeyInEffect()
    {
        Note[] notes = [new("C4", 0, 500), new("Ab4", 500, 1000), new(null, 1000, 3500), new("E4", 3500, 4000)];

        (string[] chords, string[] keys) = Run(Melody.Harmonizer(new BluesRules(), "C"), notes);

        Assert.Equal(["C7", "Bb7", "C7"], chords);
        Assert.Equal(["C", "F", "C"], keys);
    }

    [Fact]
    public void MinorKey_GivesTheSameDominantSevenths()
    {
        (string[] chords, _) = Run(Melody.Harmonizer(new BluesRules(), "A", Mode.Aeolian), Melody.Legato("A", "D", "E"));

        Assert.Equal(["A7", "D7", "E7"], chords);
    }

    [Theory]
    [InlineData("Eb")]
    [InlineData("Gb")]
    [InlineData("Bb")]
    public void BlueNoteAfterSilence_GivesI7(string note)
    {
        (string[] chords, _) = Run(Melody.Harmonizer(new BluesRules(), "C"), Melody.Legato(note));

        Assert.Equal(["C7"], chords);
    }

    [Fact]
    public void Reset_ReturnsTheBluesKey()
    {
        BluesRules rules = new();
        rules.Reset(C);
        rules.Choose(8, C, rules.Choose(0, C, null));
        Assert.Equal(new Key(5, Mode.Ionian), rules.CurrentKey(C));

        rules.Reset(C);

        Assert.Equal(C, rules.CurrentKey(C));
    }

    [Fact]
    public void PhraseReset_ReturnsTheKeyDuringTheSilence()
    {
        Harmonizer harmonizer = Melody.Harmonizer(new BluesRules(), "C");
        Note[] notes = [new("C4", 0, 500), new("Ab4", 500, 1000), new(null, 1000, 3500)];
        PitchEstimate[] estimates = Melody.Estimates(notes);
        foreach (PitchEstimate estimate in estimates.Where(estimate => estimate.TimeMs < 2900))
        {
            harmonizer.Process(estimate);
        }

        Assert.Equal(new Key(5, Mode.Ionian), harmonizer.CurrentKey);

        foreach (PitchEstimate estimate in estimates.Where(estimate => estimate.TimeMs >= 2900))
        {
            harmonizer.Process(estimate);
        }

        Assert.Equal(C, harmonizer.CurrentKey);
    }

    [Fact]
    public void ChangeOfTheAutoEstimate_ReturnsTheBluesKey()
    {
        Harmonizer harmonizer = Harmonizer.FromSettings(TestSettings.Default with { HarmonyStyle = "Blues" });
        Key g = new(7, Mode.Ionian);
        foreach (PitchEstimate estimate in KeyMelodies.Estimates(KeyMelodies.Clip("generated-melody-c-to-g-major")))
        {
            Key? before = harmonizer.Key;
            harmonizer.Process(estimate);
            if (harmonizer.Key == g && before != g)
            {
                Assert.Equal(g, harmonizer.CurrentKey);
                return;
            }
        }

        Assert.Fail("The estimate never changed to G major.");
    }

    [Fact]
    public void KeyMove_IsShownAsMajorOrMinor()
    {
        Harmonizer harmonizer = Melody.Harmonizer(new BluesRules(), "C", Mode.Lydian);

        Melody.Changes(harmonizer, Melody.Legato("C", "Ab"));

        Assert.Equal(new Key(5, Mode.Ionian), harmonizer.CurrentKey);
    }

    [Fact]
    public void OtherStyles_ReportTheKeyInEffect()
    {
        Harmonizer harmonizer = Melody.Harmonizer(new ChurchRules(), "D", Mode.Dorian);
        Melody.Changes(harmonizer, Melody.Legato("D", "Bb"));

        Assert.Equal(new Key(2, Mode.Dorian), harmonizer.CurrentKey);
    }

    [Fact]
    public void PerEstimateCallWithAKeyMove_AllocatesNothing()
    {
        PitchEstimate[] estimates = Melody.Estimates(Melody.Legato("C", "Ab", "C", "-", "-", "-", "-", "-", "Db", "E", "Eb", "B"));
        Run(Melody.Harmonizer(new BluesRules(), "C"), estimates);

        Harmonizer harmonizer = Melody.Harmonizer(new BluesRules(), "C");
        long before = GC.GetAllocatedBytesForCurrentThread();
        int changes = Run(harmonizer, estimates);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(changes >= 4);
        Assert.NotEqual(C, harmonizer.CurrentKey);
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

    // The chord and the current key's tonic in effect at the end of every note (rests left out).
    private static (string[] Chords, string[] Keys) Run(Harmonizer harmonizer, Note[] notes)
    {
        List<string> chords = [];
        List<string> keys = [];
        PitchEstimate[] estimates = Melody.Estimates(notes);
        double processedMs = 0;
        foreach (Note note in notes.Where(note => note.Name is not null))
        {
            foreach (PitchEstimate estimate in estimates.Where(estimate => estimate.TimeMs >= processedMs && estimate.TimeMs < note.EndMs))
            {
                harmonizer.Process(estimate);
            }

            processedMs = note.EndMs;

            chords.Add(harmonizer.Chord?.Symbol ?? "–");
            keys.Add(harmonizer.CurrentKey is { } key ? PitchClass.Name(key.Tonic) : "–");
        }

        return ([.. chords], [.. keys]);
    }
}
