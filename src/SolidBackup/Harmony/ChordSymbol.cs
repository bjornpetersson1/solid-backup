namespace SolidBackup.Harmony;

/// <summary>
/// Reads chord symbols as <c>docs/harmony.md</c> spells them, e.g. "Am", "G7" or "Cmaj7(#11)". A tension that is
/// already a tone of the chord, such as "Cadd9(9)", is not a chord.
/// </summary>
public static class ChordSymbol
{
    private static readonly Dictionary<string, Chord> BySymbol = BuildSymbols();

    public static bool TryParse(string? symbol, out Chord chord)
    {
        chord = default;
        return symbol is not null && BySymbol.TryGetValue(symbol, out chord);
    }

    private static Dictionary<string, Chord> BuildSymbols()
    {
        Dictionary<string, Chord> symbols = [];
        for (int root = 0; root < 12; root++)
        {
            foreach (ChordQuality quality in Enum.GetValues<ChordQuality>())
            {
                foreach (int tension in Chord.Tensions)
                {
                    if (tension != Chord.NoTension && (!TakesTensions(quality) || Chord.Intervals(quality).Contains(tension)))
                    {
                        continue;
                    }

                    Chord chord = new(root, quality, tension);
                    symbols[chord.Symbol] = chord;
                }
            }
        }

        return symbols;
    }

    // Free's three-tone shapes are named whole, and C5 with a tension would read as one of them (C5(b9), C5(#11)).
    private static bool TakesTensions(ChordQuality quality)
    {
        return quality is not ChordQuality.Power && !IsFreeShape(quality);
    }

    private static bool IsFreeShape(ChordQuality quality)
    {
        return quality >= ChordQuality.Dominant7No5;
    }
}
