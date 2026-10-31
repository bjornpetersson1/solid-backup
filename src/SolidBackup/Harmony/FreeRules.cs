using System.Numerics;

namespace SolidBackup.Harmony;

/// <summary>
/// Fri: no key. Seventh chords on every root, each allowing some tensions; the current chord stays while the note
/// is one of its tones or allowed tensions (taking the note as its tension when it is one), otherwise the chord with the note as a tone or tension is chosen by,
/// in order: most tones in common with the current chord, least movement from it, quality (maj7, m7, 7, m7b5,
/// dim7), the note as a chord tone rather than a tension, and the smallest interval from the root up to the note.
/// </summary>
public sealed class FreeRules : IChordRules
{
    // The qualities in order of preference, with the tensions each allows, as bits over the semitones above the
    // root (b9 = 1, 9 = 2, 11 = 5, #11 = 6), and its tones as the same kind of bits.
    private static readonly (ChordQuality Quality, int Tensions, int Tones)[] Shapes =
    [
        Shape(ChordQuality.Major7, 2, 6),
        Shape(ChordQuality.Minor7, 2, 5),
        Shape(ChordQuality.Dominant7, 1, 2, 6),
        Shape(ChordQuality.HalfDiminished7, 5),
        Shape(ChordQuality.Diminished7),
    ];

    public bool UsesKey => false;

    public Chord Choose(int pitchClass, Key key, Chord? current)
    {
        if (current is not { } previous)
        {
            return new Chord(pitchClass, ChordQuality.Major7);
        }

        int previousShape = ShapeOf(previous.Quality);
        int previousInterval = PitchClass.Interval(previous.Root, pitchClass);
        if (previousShape >= 0 && Allows(previousShape, previousInterval))
        {
            return IsTension(previousShape, previousInterval) ? previous with { Tension = previousInterval } : previous;
        }

        Span<int> distances = stackalloc int[12];
        NearestDistances((previous with { Tension = Chord.NoTension }).PitchClassMask, distances);

        // Each candidate's cost, lowest best: fewer tones in common, more movement, a later quality, a tension rather
        // than a chord tone, a larger interval from the root up to the note.
        int previousTones = (previous with { Tension = Chord.NoTension }).PitchClassMask;
        (int Uncommon, int Movement, int Quality, int AsTension, int Interval) best = (int.MaxValue, 0, 0, 0, 0);
        int bestRoot = 0;
        for (int quality = 0; quality < Shapes.Length; quality++)
        {
            for (int root = 0; root < 12; root++)
            {
                int interval = PitchClass.Interval(root, pitchClass);
                if (!Allows(quality, interval))
                {
                    continue;
                }

                int tones = Rotate(Shapes[quality].Tones, root);
                (int, int, int, int, int) cost = (-BitOperations.PopCount((uint)(tones & previousTones)), Movement(tones, distances), quality, IsTension(quality, interval) ? 1 : 0, interval);
                if (cost.CompareTo(best) < 0)
                {
                    best = cost;
                    bestRoot = root;
                }
            }
        }

        return new Chord(bestRoot, Shapes[best.Quality].Quality, best.AsTension == 1 ? best.Interval : Chord.NoTension);
    }

    public Key? CurrentKey(Key key)
    {
        return null;
    }

    // −1 for a quality Free does not use.
    private static int ShapeOf(ChordQuality quality)
    {
        for (int shape = 0; shape < Shapes.Length; shape++)
        {
            if (Shapes[shape].Quality == quality)
            {
                return shape;
            }
        }

        return -1;
    }

    private static (ChordQuality Quality, int Tensions, int Tones) Shape(ChordQuality quality, params int[] tensions)
    {
        return (quality, Bits(tensions), Bits(Chord.Intervals(quality)));
    }

    private static int Bits(IEnumerable<int> intervals)
    {
        return intervals.Aggregate(0, (mask, interval) => mask | (1 << interval));
    }

    private static bool Allows(int shape, int interval)
    {
        return ((Shapes[shape].Tones | Shapes[shape].Tensions) & (1 << interval)) != 0;
    }

    private static bool IsTension(int shape, int interval)
    {
        return (Shapes[shape].Tensions & (1 << interval)) != 0;
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

    private static int Rotate(int intervals, int root)
    {
        int mask = (intervals << root) | (intervals >> (12 - root));
        return mask & 0xFFF;
    }
}
