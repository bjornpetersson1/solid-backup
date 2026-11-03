using SolidBackup.Harmony;

namespace SolidBackup.Tests.Harmony;

public class ChordSymbolTests
{
    [Theory]
    [InlineData("C")]
    [InlineData("Am")]
    [InlineData("F#dim")]
    [InlineData("Bb5")]
    [InlineData("Dadd9")]
    [InlineData("Emadd9")]
    [InlineData("Gsus4")]
    [InlineData("G7")]
    [InlineData("Cmaj7")]
    [InlineData("Dm7")]
    [InlineData("Bm7b5")]
    [InlineData("Ebdim7")]
    [InlineData("Cmaj7(#11)")]
    [InlineData("C7(b9)")]
    [InlineData("Cm7(11)")]
    public void TryParse_ReadsEverySymbolTheHarmonyWrites(string symbol)
    {
        Assert.True(ChordSymbol.TryParse(symbol, out Chord chord));
        Assert.Equal(symbol, chord.Symbol);
    }

    [Theory]
    [InlineData("")]
    [InlineData("H")]
    [InlineData("C#")]
    [InlineData("am")]
    [InlineData("Cmaj9")]
    [InlineData("Cadd9(9)")]
    [InlineData("Cdim(#11)")]
    [InlineData("Csus4(11)")]
    [InlineData("Cm7b5(#11)")]
    [InlineData("C7(no5)(9)")]
    [InlineData("Caug(#11)")]
    [InlineData("C(b9,9)(#11)")]
    [InlineData(null)]
    public void TryParse_RejectsAnythingElse(string? symbol)
    {
        Assert.False(ChordSymbol.TryParse(symbol, out _));
    }

    [Theory]
    [InlineData("C5(b9)", ChordQuality.PowerFlat9)]
    [InlineData("C5(#11)", ChordQuality.PowerSharp11)]
    public void APowerChordSymbolWithATension_IsFreesShape(string symbol, ChordQuality quality)
    {
        Assert.True(ChordSymbol.TryParse(symbol, out Chord chord));
        Assert.Equal(new Chord(0, quality), chord);
    }

    [Fact]
    public void Tensions_AreExactlyTheOnesAChordAccepts()
    {
        for (int tension = 0; tension < 12; tension++)
        {
            bool listed = Chord.Tensions.Contains(tension);
            Exception? error = Record.Exception(() => new Chord(0, ChordQuality.Major7, tension));

            Assert.Equal(listed, error is null);
        }
    }
}
