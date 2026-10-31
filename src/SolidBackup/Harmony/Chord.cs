namespace SolidBackup.Harmony;

/// <summary>A chord as a root pitch class and a quality, which gives its pitch classes.</summary>
public readonly record struct Chord(int Root, ChordQuality Quality)
{
    // Semitones above the root, indexed by ChordQuality.
    private static readonly int[][] IntervalsByQuality = [[0, 4, 7], [0, 3, 7], [0, 3, 6], [0, 7], [0, 4, 7, 2], [0, 3, 7, 2], [0, 5, 7]];

    /// <summary>Bit n is set when pitch class n is in the chord; the allocation-free form for the audio path.</summary>
    public int PitchClassMask
    {
        get
        {
            int mask = 0;
            foreach (int interval in IntervalsByQuality[(int)Quality])
            {
                mask |= 1 << ((Root + interval) % 12);
            }

            return mask;
        }
    }

    /// <summary>The chord's pitch classes, root first, as a new list (for display and tests; the audio path uses <see cref="PitchClassMask"/>).</summary>
    public IReadOnlyList<int> PitchClasses
    {
        get
        {
            int root = Root;
            return [.. IntervalsByQuality[(int)Quality].Select(interval => (root + interval) % 12)];
        }
    }

    /// <summary>The symbol as <c>docs/harmony.md</c> spells it, e.g. "Am".</summary>
    public string Symbol => PitchClass.Name(Root) + Suffix(Quality);

    public bool Contains(int pitchClass)
    {
        return (PitchClassMask & (1 << pitchClass)) != 0;
    }

    public override string ToString()
    {
        return Symbol;
    }

    private static string Suffix(ChordQuality quality)
    {
        return quality switch
        {
            ChordQuality.Major => "",
            ChordQuality.Minor => "m",
            ChordQuality.Diminished => "dim",
            ChordQuality.Power => "5",
            ChordQuality.Add9 => "add9",
            ChordQuality.MinorAdd9 => "madd9",
            ChordQuality.Sus4 => "sus4",
            _ => throw new ArgumentOutOfRangeException(nameof(quality)),
        };
    }
}
