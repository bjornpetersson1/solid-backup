using SolidBackup.Harmony;
using SolidBackup.Synthesis;

namespace SolidBackup.Tests.Synthesis;

/// <summary>Chords for the tests, from their symbols.</summary>
internal static class TestChords
{
    public static Chord Parse(string symbol)
    {
        Assert.True(ChordSymbol.TryParse(symbol, out Chord chord), $"\"{symbol}\" is no chord symbol.");
        return chord;
    }

    /// <summary>The chord in close voicing, shaped.</summary>
    public static int[] Shaped(Chord chord, ChordShape shape)
    {
        Span<int> voiced = stackalloc int[ChordVoicing.MaxTones];
        int voicedCount = ChordVoicing.Voice(chord, voiced);
        Span<int> notes = stackalloc int[ChordVoicing.MaxShapedTones];
        int count = ChordVoicing.Shape(chord, voiced[..voicedCount], shape, notes);
        return notes[..count].ToArray();
    }
}
