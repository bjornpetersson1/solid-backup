using SolidBackup.Harmony;

namespace SolidBackup.Tests.Harmony;

/// <summary>The timing examples T1–T6 in <c>docs/harmony.md</c> (Church, C Ionian).</summary>
public sealed class HarmonizerTimingTests
{
    [Theory]
    [InlineData(48_000, 64)]
    [InlineData(44_100, 64)]
    [InlineData(48_000, 512)]
    public void T1_FirstNoteAfterSilence_ChoosesAtOnce(int sampleRate, int bufferSize)
    {
        Assert.Equal([(0.0, "C")], Changes([new Note("C4", 0, 500)], sampleRate, bufferSize));
    }

    [Theory]
    [InlineData(48_000, 64)]
    [InlineData(44_100, 64)]
    [InlineData(48_000, 512)]
    public void T2_PassingNoteShorterThanTheHold_ChangesNothing(int sampleRate, int bufferSize)
    {
        Note[] notes = [new("C4", 0, 500), new("D4", 500, 580), new("C4", 580, 1000)];

        Assert.Equal([(0.0, "C")], Changes(notes, sampleRate, bufferSize));
    }

    [Theory]
    [InlineData(48_000, 64)]
    [InlineData(44_100, 64)]
    [InlineData(48_000, 512)]
    public void T3_NewNote_ChangesOnceItHasLastedTheHold(int sampleRate, int bufferSize)
    {
        Note[] notes = [new("C4", 0, 500), new("B4", 500, 1000)];

        AssertChangesAt(Changes(notes, sampleRate, bufferSize), [(0, "C"), (620, "G")], sampleRate, bufferSize);
    }

    [Theory]
    [InlineData(48_000, 64)]
    [InlineData(44_100, 64)]
    [InlineData(48_000, 512)]
    public void T4_ChangeWaitsForTheMinimumChordDuration(int sampleRate, int bufferSize)
    {
        Note[] notes = [new("C4", 0, 50), new("B4", 50, 500)];

        AssertChangesAt(Changes(notes, sampleRate, bufferSize), [(0, "C"), (250, "G")], sampleRate, bufferSize);
    }

    [Theory]
    [InlineData(48_000, 64)]
    [InlineData(44_100, 64)]
    [InlineData(48_000, 512)]
    public void T5_ShortSilence_RemembersTheChord(int sampleRate, int bufferSize)
    {
        Note[] notes = [new("B4", 0, 500), new("A4", 500, 1000), new(null, 1000, 1800), new("C4", 1800, 2300)];

        Assert.Equal(["G", "Am"], Symbols(Changes(notes, sampleRate, bufferSize)));
    }

    [Theory]
    [InlineData(48_000, 64)]
    [InlineData(44_100, 64)]
    [InlineData(48_000, 512)]
    public void T6_SilenceOfAPhraseReset_ChoosesAfresh(int sampleRate, int bufferSize)
    {
        Note[] notes = [new("B4", 0, 500), new("A4", 500, 1000), new(null, 1000, 3500), new("C4", 3500, 4000)];

        List<(double TimeMs, string Symbol)> changes = Changes(notes, sampleRate, bufferSize);

        Assert.Equal(["G", "Am", "C"], Symbols(changes));
        Assert.InRange(changes[^1].TimeMs, 3500, 3500 + StepMs(sampleRate, bufferSize));
    }

    [Fact]
    public void Silence_KeepsTheChord()
    {
        Harmonizer harmonizer = Melody.Harmonizer(new ChurchRules(), "C");
        Melody.Changes(harmonizer, Melody.Legato("B4", "-", "-"));

        Assert.Equal("G", harmonizer.Chord?.Symbol);
    }

    private static List<(double TimeMs, string Symbol)> Changes(Note[] notes, int sampleRate, int bufferSize)
    {
        return [.. Melody.Changes(Melody.Harmonizer(new ChurchRules(), "C"), notes, sampleRate, bufferSize)
            .Select(change => (change.TimeMs, change.Chord.Symbol))];
    }

    private static string[] Symbols(List<(double TimeMs, string Symbol)> changes)
    {
        return [.. changes.Select(change => change.Symbol)];
    }

    // Each change comes with the first estimate at or after its expected time.
    private static void AssertChangesAt(List<(double TimeMs, string Symbol)> changes, (double TimeMs, string Symbol)[] expected, int sampleRate, int bufferSize)
    {
        Assert.Equal(expected.Select(change => change.Symbol), Symbols(changes));
        foreach (((double actual, _), (double time, _)) in changes.Zip(expected))
        {
            Assert.InRange(actual, time, time + StepMs(sampleRate, bufferSize));
        }
    }

    private static double StepMs(int sampleRate, int bufferSize)
    {
        return bufferSize * 1000.0 / sampleRate;
    }
}
