namespace SolidBackup.Harmony;

/// <summary>What makes a key a candidate when a style moves its key: e.g. one of its chords, or its scale, has the note.</summary>
internal interface IKeyTest
{
    bool Has(Key key, int pitchClass);
}

/// <summary>Where the styles that move their key (Blues, Jazz) move it: the nearest key on the circle of fifths, the flat side first.</summary>
internal static class CircleOfFifths
{
    // One step round the circle: a fourth up is the flat side, tried first, and a fifth up the sharp side.
    private static readonly int[] Steps = [5, 7];

    /// <summary>The nearest key to <paramref name="start"/>, in the same mode, that passes <paramref name="test"/> for the note; generic over a struct test, so it allocates nothing.</summary>
    public static Key NearestKeyWith<TTest>(TTest test, Key start, int pitchClass)
        where TTest : struct, IKeyTest
    {
        for (int distance = 1; distance <= 6; distance++)
        {
            foreach (int interval in Steps)
            {
                Key candidate = start with { Tonic = (start.Tonic + (interval * distance)) % 12 };
                if (test.Has(candidate, pitchClass))
                {
                    return candidate;
                }
            }
        }

        throw new InvalidOperationException("Every pitch class is in some key.");
    }

    /// <summary>A key whose chords in a style contain the note.</summary>
    public readonly struct InChords(ScaleRules chords) : IKeyTest
    {
        public bool Has(Key key, int pitchClass)
        {
            return chords.ContainsNote(key, pitchClass);
        }
    }

    /// <summary>A key whose scale contains the note.</summary>
    public readonly struct InScale : IKeyTest
    {
        public bool Has(Key key, int pitchClass)
        {
            for (int degree = 1; degree <= 7; degree++)
            {
                if (key.PitchClassOf(degree) == pitchClass)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
