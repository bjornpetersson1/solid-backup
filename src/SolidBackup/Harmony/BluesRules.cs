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

    // One step round the circle of fifths: a fourth up is the flat side, tried first, and a fifth up the sharp side.
    private static readonly int[] FifthsSteps = [5, 7];

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

        if (!DominantSevenths.Contain(_bluesKey, pitchClass))
        {
            _bluesKey = NearestKeyWith(pitchClass);
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

    private Key NearestKeyWith(int pitchClass)
    {
        for (int steps = 1; steps <= 6; steps++)
        {
            foreach (int interval in FifthsSteps)
            {
                Key candidate = _bluesKey with { Tonic = (_bluesKey.Tonic + (interval * steps)) % 12 };
                if (DominantSevenths.Contain(candidate, pitchClass))
                {
                    return candidate;
                }
            }
        }

        throw new InvalidOperationException("Every pitch class is in some key's I7.");
    }

    /// <summary>I7, IV7 and V7 with priority 1, 4, 5, and the common successors, in any mode.</summary>
    private sealed class DominantSevenths() : ScaleRules([1, 4, 5], SeventhOn)
    {
        // Bit n set when the pitch class n semitones above the tonic is in I7 (0 4 7 10), IV7 (5 9 0 3) or V7 (7 11 2 5).
        private const int Tones = (1 << 0) | (1 << 2) | (1 << 3) | (1 << 4) | (1 << 5) | (1 << 7) | (1 << 9) | (1 << 10) | (1 << 11);

        public static bool Contain(Key key, int pitchClass)
        {
            return (Tones & (1 << PitchClass.Interval(key.Tonic, pitchClass))) != 0;
        }

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
