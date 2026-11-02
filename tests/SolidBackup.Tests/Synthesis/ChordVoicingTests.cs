using SolidBackup.Harmony;
using SolidBackup.Synthesis;

namespace SolidBackup.Tests.Synthesis;

public class ChordVoicingTests
{
    [Theory]
    [InlineData("C", new[] { 48, 52, 55 })]
    [InlineData("Am", new[] { 57, 60, 64 })]
    [InlineData("Bm7b5", new[] { 59, 62, 65, 69 })]
    [InlineData("Cmaj7(#11)", new[] { 48, 52, 55, 59, 66 })]
    [InlineData("Cm7", new[] { 48, 51, 55, 58 })]
    [InlineData("F#dim", new[] { 54, 57, 60 })]
    [InlineData("Cadd9", new[] { 48, 52, 55, 62 })]
    [InlineData("Bmaj7(#11)", new[] { 59, 63, 65, 66, 70 })]
    [InlineData("Bbm7(11)", new[] { 58, 61, 63, 65, 68 })]
    public void Voice_PutsTheRootInOctaveThreeAndStacksEachToneNearestAbove(string symbol, int[] expected)
    {
        Chord chord = TestChords.Parse(symbol);
        Span<int> notes = stackalloc int[ChordVoicing.MaxTones];

        int count = ChordVoicing.Voice(chord, notes);

        Assert.Equal(expected, notes[..count].ToArray());
    }

    [Fact]
    public void Voice_EveryChordStaysWithinC3ToC5()
    {
        Span<int> notes = stackalloc int[ChordVoicing.MaxTones];
        for (int root = 0; root < 12; root++)
        {
            foreach (ChordQuality quality in Enum.GetValues<ChordQuality>())
            {
                foreach (int tension in Chord.Tensions)
                {
                    int count = ChordVoicing.Voice(new Chord(root, quality, tension), notes);

                    Assert.All(notes[..count].ToArray(), note => Assert.InRange(note, ChordVoicing.LowestNote, ChordVoicing.HighestNote));
                    Assert.Equal(count, notes[..count].ToArray().Distinct().Count());
                }
            }
        }
    }

    [Fact]
    public void Voice_EveryRootLiesBetweenC3AndB3()
    {
        Span<int> notes = stackalloc int[ChordVoicing.MaxTones];
        for (int root = 0; root < 12; root++)
        {
            ChordVoicing.Voice(new Chord(root, ChordQuality.Major), notes);

            Assert.InRange(notes[0], 48, 59);
        }
    }

    [Theory]
    [InlineData("C", "Am", new[] { 48, 52, 57 })]
    [InlineData("C", "F#dim", new[] { 48, 54, 57 })]
    [InlineData("C", "G", new[] { 50, 55, 59 })]
    [InlineData("C", "F", new[] { 48, 53, 57 })]
    [InlineData("C", "Cmaj7(#11)", new[] { 48, 52, 54, 55, 59 })]
    [InlineData("Cmaj7", "Dm7", new[] { 48, 50, 53, 57 })]
    public void Lead_KeepsSharedTonesAndMovesNewOnesNearestTheLeavingOnes(string from, string to, int[] expected)
    {
        Span<int> previous = stackalloc int[ChordVoicing.MaxTones];
        int previousCount = ChordVoicing.Voice(TestChords.Parse(from), previous);
        Span<int> notes = stackalloc int[ChordVoicing.MaxTones];

        int count = ChordVoicing.Lead(TestChords.Parse(to), previous[..previousCount], notes);

        Assert.Equal(expected, notes[..count].ToArray());
    }

    [Fact]
    public void Lead_WithoutAPreviousChord_IsCloseVoicing()
    {
        Span<int> notes = stackalloc int[ChordVoicing.MaxTones];

        int count = ChordVoicing.Lead(TestChords.Parse("Am"), [], notes);

        Assert.Equal(new[] { 57, 60, 64 }, notes[..count].ToArray());
    }

    [Fact]
    public void Lead_ThroughALongProgression_StaysWithinC3ToC5()
    {
        string[] symbols = ["C", "Am", "F", "G", "Em", "Bm7b5", "E7", "Am", "Dm7", "G7", "Cmaj7(#11)", "F#dim", "Bb", "Eb", "Ab"];
        Span<int> previous = stackalloc int[ChordVoicing.MaxTones];
        Span<int> notes = stackalloc int[ChordVoicing.MaxTones];
        int previousCount = 0;
        foreach (string symbol in symbols)
        {
            int count = ChordVoicing.Lead(TestChords.Parse(symbol), previous[..previousCount], notes);
            foreach (int note in notes[..count])
            {
                Assert.InRange(note, ChordVoicing.LowestNote, ChordVoicing.HighestNote);
            }

            notes[..count].CopyTo(previous);
            previousCount = count;
        }
    }
}
