namespace SolidBackup.Harmony;

/// <summary>
/// A chord as a root pitch class and a quality, which gives its pitch classes, and optionally a tension added on
/// top (Free's dissonance), in semitones above the root: b9 = 1, 9 = 2, 11 = 5, #11 = 6.
/// </summary>
public readonly record struct Chord(int Root, ChordQuality Quality, int Tension = Chord.NoTension)
{
    /// <summary>No tension; 0, so a default chord has none.</summary>
    public const int NoTension = 0;

    public int Tension { get; init; } = Tension is NoTension or 1 or 2 or 5 or 6
        ? Tension
        : throw new ArgumentOutOfRangeException(nameof(Tension), Tension, "A tension is b9 (1), 9 (2), 11 (5) or #11 (6).");

    // Semitones above the root, indexed by ChordQuality.
    private static readonly int[][] IntervalsByQuality = [[0, 4, 7], [0, 3, 7], [0, 3, 6], [0, 7], [0, 4, 7, 2], [0, 3, 7, 2], [0, 5, 7], [0, 4, 7, 10], [0, 4, 7, 11], [0, 3, 7, 10], [0, 3, 6, 10], [0, 3, 6, 9]];

    /// <summary>No tension and every tension a chord may have, as the check on <see cref="Tension"/> accepts them.</summary>
    public static IReadOnlyList<int> Tensions { get; } = Array.AsReadOnly([NoTension, 1, 2, 5, 6]);

    /// <summary>Bit n is set when pitch class n is in the chord; the allocation-free form for the audio path.</summary>
    public int PitchClassMask
    {
        get
        {
            int mask = Tension == NoTension ? 0 : 1 << ((Root + Tension) % 12);
            foreach (int interval in IntervalsByQuality[(int)Quality])
            {
                mask |= 1 << ((Root + interval) % 12);
            }

            return mask;
        }
    }

    /// <summary>
    /// The chord's pitch classes, root first and the tension last, as a new list (for display and tests; the audio
    /// path uses <see cref="PitchClassMask"/>).
    /// </summary>
    public IReadOnlyList<int> PitchClasses
    {
        get
        {
            int root = Root;
            IEnumerable<int> intervals = Tension == NoTension ? IntervalsByQuality[(int)Quality] : [.. IntervalsByQuality[(int)Quality], Tension];
            return [.. intervals.Select(interval => (root + interval) % 12)];
        }
    }

    /// <summary>The symbol as <c>docs/harmony.md</c> spells it, e.g. "Am" or "Cmaj7(#11)".</summary>
    public string Symbol => PitchClass.Name(Root) + Suffix(Quality) + (Tension == NoTension ? "" : $"({TensionName(Tension)})");

    /// <summary>The semitones above the root of the quality's tones, root first.</summary>
    public static IReadOnlyList<int> Intervals(ChordQuality quality)
    {
        return IntervalsByQuality[(int)quality];
    }

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
            ChordQuality.Dominant7 => "7",
            ChordQuality.Major7 => "maj7",
            ChordQuality.Minor7 => "m7",
            ChordQuality.HalfDiminished7 => "m7b5",
            ChordQuality.Diminished7 => "dim7",
            _ => throw new ArgumentOutOfRangeException(nameof(quality)),
        };
    }

    private static string TensionName(int tension)
    {
        return tension switch
        {
            1 => "b9",
            2 => "9",
            5 => "11",
            6 => "#11",
            _ => throw new ArgumentOutOfRangeException(nameof(tension)),
        };
    }
}
