using System.Numerics;

namespace SolidBackup.Harmony;

/// <summary>
/// Fri (<c>docs/harmony.md</c>, E7-4): no key. Every set of three pitch classes, named by the first of the 19 shapes in
/// <see cref="Shapes"/> it matches, rooted on the shape's first tone. The current chord stays while it contains the
/// note; otherwise the chord with the note is chosen by, in order: most tones in common with the current chord, least
/// movement from it, the earlier shape, and the smallest interval from the root up to the note. After a change that
/// kept the root, the next change must move it. Keeps that as state, so each harmonizer needs its own instance;
/// allocates nothing per note.
/// </summary>
public sealed class FreeRules : IChordRules
{
    // The shapes as an array, for the search on the audio thread.
    private static readonly ChordQuality[] ShapeArray =
    [
        ChordQuality.Major,
        ChordQuality.Minor,
        ChordQuality.Sus4,
        ChordQuality.Dominant7No5,
        ChordQuality.Minor7No5,
        ChordQuality.Major7No5,
        ChordQuality.Add9No5,
        ChordQuality.MinorAdd9No5,
        ChordQuality.Diminished,
        ChordQuality.Augmented,
        ChordQuality.Dominant7No3,
        ChordQuality.Major7No3,
        ChordQuality.Sharp11No5,
        ChordQuality.Sharp9No5,
        ChordQuality.MinorMajor7No5,
        ChordQuality.MinorFlat9No5,
        ChordQuality.PowerFlat9,
        ChordQuality.PowerSharp11,
        ChordQuality.Cluster,
    ];

    /// <summary>The three-tone shapes, in order of preference; together they cover every set of three pitch classes once.</summary>
    public static IReadOnlyList<ChordQuality> Shapes { get; } = Array.AsReadOnly(ShapeArray);

    // Whether the last chord change kept the root, so the next one must move it, and the chord that change gave; a
    // current chord other than that one (not chosen here) starts the rule afresh.
    private bool _keptRoot;
    private Chord? _chosen;

    public bool UsesKey => false;

    public void Reset(Key key)
    {
        _keptRoot = false;
        _chosen = null;
    }

    public Chord Choose(int pitchClass, Key key, Chord? current)
    {
        if (current is not { } previous)
        {
            _keptRoot = false;
            _chosen = new Chord(pitchClass, ChordQuality.Major);
            return _chosen.Value;
        }

        if (previous != _chosen)
        {
            _keptRoot = false;
        }

        if (previous.Contains(pitchClass))
        {
            return previous;
        }

        int previousTones = previous.PitchClassMask;

        Span<int> distances = stackalloc int[12];
        NearestDistances(previousTones, distances);

        // Each candidate's cost, lowest best: fewer tones in common, more movement, a later shape, a larger interval
        // from the root up to the note.
        (int Uncommon, int Movement, int Shape, int Interval) best = (int.MaxValue, 0, 0, 0);
        int bestRoot = 0;
        for (int shape = 0; shape < ShapeArray.Length; shape++)
        {
            // Only the roots that put one of the shape's tones on the note; indexed, as an enumerator would allocate.
            IReadOnlyList<int> intervals = Chord.Intervals(ShapeArray[shape]);
            for (int tone = 0; tone < intervals.Count; tone++)
            {
                int interval = intervals[tone];
                int root = (pitchClass - interval + 12) % 12;
                if (_keptRoot && root == previous.Root)
                {
                    continue;
                }

                int tones = new Chord(root, ShapeArray[shape]).PitchClassMask;
                (int, int, int, int) cost = (-BitOperations.PopCount((uint)(tones & previousTones)), Movement(tones, distances), shape, interval);
                if (cost.CompareTo(best) < 0)
                {
                    best = cost;
                    bestRoot = root;
                }
            }
        }

        Chord chosen = new(bestRoot, ShapeArray[best.Shape]);
        _keptRoot = chosen.Root == previous.Root;
        _chosen = chosen;
        return chosen;
    }

    public Key? CurrentKey(Key key)
    {
        return null;
    }

    // For every pitch class, its distance in semitones (0–6) to the nearest previous tone.
    private static void NearestDistances(int previousTones, Span<int> distances)
    {
        for (int pitchClass = 0; pitchClass < 12; pitchClass++)
        {
            distances[pitchClass] = 6;
            for (int other = 0; other < 12; other++)
            {
                if ((previousTones & (1 << other)) != 0)
                {
                    distances[pitchClass] = Math.Min(distances[pitchClass], Math.Min(PitchClass.Interval(pitchClass, other), PitchClass.Interval(other, pitchClass)));
                }
            }
        }
    }

    // The sum, over the candidate's tones, of each tone's distance to the nearest previous tone.
    private static int Movement(int tones, ReadOnlySpan<int> distances)
    {
        int sum = 0;
        for (int tone = 0; tone < 12; tone++)
        {
            if ((tones & (1 << tone)) != 0)
            {
                sum += distances[tone];
            }
        }

        return sum;
    }
}
