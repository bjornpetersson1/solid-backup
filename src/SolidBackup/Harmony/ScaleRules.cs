namespace SolidBackup.Harmony;

/// <summary>
/// The common choice in <c>docs/harmony.md</c> for the styles built on scale degrees: keep the current chord
/// if it contains the note, else the first successor of the current chord that does, else the first chord in
/// the style's priority order that does, else keep the current chord (or the tonic chord when there is none).
/// The chords of every key are built in the constructor, so choosing allocates nothing. A style may add colours
/// to the current chord (after keep) and borrowed chords (before the last keep).
/// </summary>
public abstract class ScaleRules : IChordRules
{
    // The ordered successors of each degree, indexed 1 to 7.
    private static readonly int[][] Successors = [[], [], [5], [6], [5, 1], [1, 6], [4, 2], [1]];

    private readonly int[] _priority;

    // Indexed by key (tonic × mode) and degree 1 to 7; null where the style leaves a degree out.
    private readonly Chord?[][] _chords;

    // Indexed by key: bit n is set when pitch class n is in one of the style's chords in that key.
    private readonly int[] _notes;

    /// <param name="priority">The degrees the style uses, most preferred first.</param>
    /// <param name="chordOn">The style's chord on a degree of a key, or null when it leaves that degree out.</param>
    protected ScaleRules(int[] priority, Func<Key, int, Chord?> chordOn)
    {
        _priority = priority;
        _chords = [.. Key.All.Select(key => (Chord?[])[null, .. Enumerable.Range(1, 7).Select(degree => priority.Contains(degree) ? chordOn(key, degree) : null)])];
        _notes = [.. _chords.Select(chords => chords.OfType<Chord>().Aggregate(0, (notes, chord) => notes | chord.PitchClassMask))];
    }

    protected static int[] DefaultPriority { get; } = [1, 5, 4, 6, 2, 3, 7];

    public Chord Choose(int pitchClass, Key key, Chord? current)
    {
        if (current is { } kept && kept.Contains(pitchClass))
        {
            return kept;
        }

        Chord?[] chords = _chords[key.Index];
        if (current is { } previous)
        {
            int degree = DegreeOf(chords, Uncoloured(previous));
            if (degree > 0 && Coloured(pitchClass, previous) is { } coloured)
            {
                return coloured;
            }

            foreach (int successor in Successors[degree])
            {
                if (chords[successor] is { } next && next.Contains(pitchClass))
                {
                    return next;
                }
            }
        }

        foreach (int degree in _priority)
        {
            if (chords[degree] is { } candidate && candidate.Contains(pitchClass))
            {
                return candidate;
            }
        }

        return Borrowed(pitchClass, key) ?? current ?? chords[1] ?? throw new InvalidOperationException("Every style has a tonic chord.");
    }

    /// <summary>The style's chord on a degree of a key, 1 to 7, or null where the style leaves it out.</summary>
    public Chord? ChordOn(Key key, int degree)
    {
        return _chords[key.Index][degree];
    }

    /// <summary>Whether a note is in one of the style's chords in the key; a note that is not is foreign to it.</summary>
    public bool ContainsNote(Key key, int pitchClass)
    {
        return (_notes[key.Index] & (1 << pitchClass)) != 0;
    }

    /// <summary>The degree (1 to 7) of one of the style's chords in a key, or 0 when it is not one of them.</summary>
    public int DegreeOf(Key key, Chord chord)
    {
        return DegreeOf(_chords[key.Index], chord);
    }

    /// <summary>The style's chords in a key, in priority order.</summary>
    public IReadOnlyList<Chord> ChordsIn(Key key)
    {
        Chord?[] chords = _chords[key.Index];
        return [.. _priority.Select(degree => chords[degree]).OfType<Chord>()];
    }

    /// <summary>The style's own chord that a coloured chord was made from; the chord itself when the style has no colours.</summary>
    protected virtual Chord Uncoloured(Chord chord)
    {
        return chord;
    }

    /// <summary>The current chord (one of the style's, perhaps coloured) coloured to fit the note, or null for no colour.</summary>
    protected virtual Chord? Coloured(int pitchClass, Chord current)
    {
        return null;
    }

    /// <summary>A borrowed chord that contains the note, tried when none of the style's chords does; null by default.</summary>
    protected virtual Chord? Borrowed(int pitchClass, Key key)
    {
        return null;
    }

    // 0, whose successor list is empty, for a chord that is not on any of the style's degrees.
    private static int DegreeOf(Chord?[] chords, Chord chord)
    {
        return Math.Max(0, Array.IndexOf(chords, chord));
    }
}
