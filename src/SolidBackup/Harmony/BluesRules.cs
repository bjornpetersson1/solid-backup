namespace SolidBackup.Harmony;

/// <summary>
/// Blues: I7, IV7 and V7 of the current blues key. The blue notes (minor third, diminished fifth and minor
/// seventh) keep the chord; any other note outside the three chords moves the blues key to the nearest key on the
/// circle of fifths whose I7, IV7 or V7 contains it, the flat side first. <see cref="Reset"/>, which the engine
/// calls on a phrase reset and when the key in effect changes, returns it. Keeps the blues key as state, so each
/// harmonizer needs its own instance.
/// </summary>
public sealed class BluesRules : IChordRules
{
    // The blue notes, in semitones above the blues key's tonic.
    private const int MinorThird = 3;
    private const int DiminishedFifth = 6;
    private const int MinorSeventh = 10;

    private readonly ScaleRules _chords = new DominantSevenths();

    private Key _keyInEffect;
    private Key _bluesKey;
    private bool _moved;

    public Chord Choose(int pitchClass, Key key, Chord? current)
    {
        // For a caller that did not report the key through Reset.
        if (key != _keyInEffect)
        {
            Reset(key);
        }

        if (PitchClass.Interval(_bluesKey.Tonic, pitchClass) is MinorThird or DiminishedFifth or MinorSeventh)
        {
            return current ?? new Chord(_bluesKey.Tonic, ChordQuality.Dominant7);
        }

        if (!_chords.ContainsNote(_bluesKey, pitchClass))
        {
            _bluesKey = CircleOfFifths.NearestKeyWith(new CircleOfFifths.InChords(_chords), _bluesKey, pitchClass);
            _moved = true;
        }

        return _chords.Choose(pitchClass, _bluesKey, current);
    }

    /// <summary>The moved blues key, shown as major or minor like the other styles without modes; else the key in effect.</summary>
    public Key? CurrentKey(Key key)
    {
        return _moved && key == _keyInEffect ? _bluesKey : key;
    }

    public void Reset(Key key)
    {
        _keyInEffect = key;
        _bluesKey = key.MajorOrMinor;
        _moved = false;
    }

    /// <summary>I7, IV7 and V7 with priority 1, 4, 5, and the common successors, in any mode.</summary>
    private sealed class DominantSevenths() : ScaleRules([1, 4, 5], SeventhOn)
    {
        private static Chord? SeventhOn(Key key, int degree)
        {
            int? root = degree switch
            {
                1 => 0,
                4 => 5,
                5 => 7,
                _ => null,
            };
            return root is { } interval ? new Chord((key.Tonic + interval) % 12, ChordQuality.Dominant7) : null;
        }
    }
}
