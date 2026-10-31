namespace SolidBackup.Harmony;

/// <summary>
/// Pop: I, V, vi and IV in major, i, bVII, bVI and bIII in minor; the current chord takes an add9 or sus4
/// colour before it changes, and a note no chord contains gets a borrowed chord from the parallel key
/// (bVII, iv, bVI in major; V, IV in minor). A borrowed chord is followed by the tonic through the priority
/// order, which starts with it.
/// </summary>
public sealed class PopRules : ScaleRules
{
    // Indexed by Key.Index: the borrowed chords of each key, in the order they are tried.
    private readonly Chord[][] _borrowed;

    public PopRules()
        : base([1, 5, 7, 6, 4, 3], TriadOn)
    {
        _borrowed = [.. Key.All.Select(key => key.IsMajor
            ? new[] { 7, 4, 6 }.Select(new Key(key.Tonic, Mode.Aeolian).TriadOn).ToArray()
            : new[] { 5, 4 }.Select(new Key(key.Tonic, Mode.Ionian).TriadOn).ToArray())];
    }

    protected override Chord Uncoloured(Chord chord)
    {
        return chord.Quality switch
        {
            ChordQuality.Add9 or ChordQuality.Sus4 => chord with { Quality = ChordQuality.Major },
            ChordQuality.MinorAdd9 => chord with { Quality = ChordQuality.Minor },
            _ => chord,
        };
    }

    protected override Chord? Coloured(int pitchClass, Chord current)
    {
        Chord triad = Uncoloured(current);
        int interval = PitchClass.Interval(triad.Root, pitchClass);
        return (interval, triad.Quality) switch
        {
            (2, ChordQuality.Major) => triad with { Quality = ChordQuality.Add9 },
            (2, ChordQuality.Minor) => triad with { Quality = ChordQuality.MinorAdd9 },
            (5, ChordQuality.Major) => triad with { Quality = ChordQuality.Sus4 },
            _ => null,
        };
    }

    protected override Chord? Borrowed(int pitchClass, Key key)
    {
        foreach (Chord chord in _borrowed[key.Index])
        {
            if (chord.Contains(pitchClass))
            {
                return chord;
            }
        }

        return null;
    }

    // Major uses degrees 1, 5, 6, 4 of Ionian; minor uses 1, 7, 6, 3 of Aeolian (i, bVII, bVI, bIII). The priority
    // list above interleaves the two so each keeps its own order.
    private static Chord? TriadOn(Key key, int degree)
    {
        bool inSet = key.IsMajor ? degree is 1 or 5 or 6 or 4 : degree is 1 or 7 or 6 or 3;
        return inSet ? key.MajorOrMinor.TriadOn(degree) : null;
    }
}
