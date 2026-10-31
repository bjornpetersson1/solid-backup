using SolidBackup.Harmony;

namespace SolidBackup.Tests.Harmony;

public sealed class ChordTests
{
    [Theory]
    [InlineData(0, "C")]
    [InlineData(1, "Db")]
    [InlineData(2, "D")]
    [InlineData(3, "Eb")]
    [InlineData(4, "E")]
    [InlineData(5, "F")]
    [InlineData(6, "F#")]
    [InlineData(7, "G")]
    [InlineData(8, "Ab")]
    [InlineData(9, "A")]
    [InlineData(10, "Bb")]
    [InlineData(11, "B")]
    public void Root_IsSpelledAsTheDocSays(int root, string symbol)
    {
        Assert.Equal(symbol, new Chord(root, ChordQuality.Major).Symbol);
    }

    [Theory]
    [InlineData(ChordQuality.Major, "A", new[] { 9, 1, 4 })]
    [InlineData(ChordQuality.Minor, "Am", new[] { 9, 0, 4 })]
    [InlineData(ChordQuality.Diminished, "Adim", new[] { 9, 0, 3 })]
    [InlineData(ChordQuality.Power, "A5", new[] { 9, 4 })]
    public void Quality_GivesSymbolAndPitchClasses(ChordQuality quality, string symbol, int[] pitchClasses)
    {
        Chord chord = new(9, quality);

        Assert.Equal(symbol, chord.Symbol);
        Assert.Equal(pitchClasses, chord.PitchClasses);
        Assert.All(Enumerable.Range(0, 12), pitchClass => Assert.Equal(pitchClasses.Contains(pitchClass), chord.Contains(pitchClass)));
    }

    [Fact]
    public void Key_PrintsTonicAndMode()
    {
        Assert.Equal("F# Dorian", new Key(6, Mode.Dorian).ToString());
    }

    [Theory]
    [InlineData("C", 0)]
    [InlineData("F#", 6)]
    [InlineData("Bb", 10)]
    [InlineData("B", 11)]
    [InlineData("Cb", 11)]
    public void PitchClass_ParsesTonics(string name, int expected)
    {
        Assert.True(PitchClass.TryParse(name, out int pitchClass));
        Assert.Equal(expected, pitchClass);
    }

    [Theory]
    [InlineData("H")]
    [InlineData("C4")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("auto")]
    [InlineData("c")]
    [InlineData("bb")]
    public void PitchClass_RejectsOtherText(string? name)
    {
        Assert.False(PitchClass.TryParse(name, out _));
    }
}
