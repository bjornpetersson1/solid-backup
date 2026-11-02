using SolidBackup.Rhythm;

namespace SolidBackup.Harmony;

/// <summary>
/// Jazz, as <c>docs/harmony.md</c> describes it. The diatonic seventh chords are chosen with the common rules.
/// The tonic starts a loop I – VI – II – V on every other beat of the tempo grid, which goes on through silence;
/// while the tonic sounds, V7 takes it as its 11th. A first foreign note in a phrase gives a secondary dominant,
/// followed by its target; the second moves the key to the nearest one with that note and plays II – V – I there,
/// one chord per beat. The key goes back on a phrase reset. Keeps the loop, cadence and moved key as state, so each
/// harmonizer needs its own instance; allocates nothing per note.
/// </summary>
public sealed class JazzRules(TempoGrid grid) : IChordRules
{
    private const int Eleventh = 5;

    // The loop's degrees, starting on the tonic.
    private static readonly int[] LoopDegrees = [1, 6, 2, 5];

    // The diatonic chords a secondary dominant may lead to, the strongest resolution first (the default priority without the tonic).
    private static readonly int[] SecondaryTargets = [5, 4, 6, 2, 3, 7];

    private readonly JazzSevenths _chords = new();
    private readonly double _beatMs = grid.BeatMs;

    private Key _keyInEffect;
    private Key _key;
    private bool _moved;
    private int _foreignNotes;
    private Chord? _target;

    private bool _looping;
    private bool _loopAnchored;
    private Key _loopKey;
    private int _loopIndex;
    private double _nextLoopMs;

    private CadenceStep _cadence;
    private double _cadenceAtMs;
    private Chord _cadenceDominant;
    private Chord _cadenceTonic;

    private enum CadenceStep
    {
        None,
        Dominant,
        Tonic,
    }

    public bool KeepsPlaying => _looping;

    public Key? CurrentKey(Key key)
    {
        return _moved && key == _keyInEffect ? _key : key;
    }

    /// <summary>
    /// A new key in effect stops the loop. A phrase reset returns the key; as the loop never stops in silence, it goes
    /// on in the returned key, and a cadence still under way is dropped.
    /// </summary>
    public void Reset(Key key)
    {
        if (key != _keyInEffect)
        {
            _looping = false;
        }

        _cadence = CadenceStep.None;
        _keyInEffect = key;
        _key = key.MajorOrMinor;
        _loopKey = _key;
        _moved = false;
        _foreignNotes = 0;
        _target = null;
    }

    public Chord Choose(int pitchClass, Key key, Chord? current)
    {
        if (key != _keyInEffect)
        {
            Reset(key);
        }

        Chord tonic = On(_key, 1);
        if (_cadence != CadenceStep.None)
        {
            // II – V – I is played through, whatever the melody does.
            return current ?? tonic;
        }

        if (pitchClass == _key.Tonic)
        {
            if (!_looping)
            {
                _looping = true;
                _loopAnchored = false;
                _loopKey = _key;
                _loopIndex = 0;
                _target = null;
                return tonic;
            }

            // The loop goes on in its order; its chord holds the tonic (V7 as the 11th).
            return LoopChord(pitchClass);
        }

        _looping = false;

        // The 11th belongs to the loop only; the common rules see the plain seventh chord.
        Chord? plain = current is { } chord ? chord with { Tension = Chord.NoTension } : null;
        if (_chords.ContainsNote(_key, pitchClass))
        {
            if (plain is { } kept && kept.Contains(pitchClass))
            {
                return kept;
            }

            Chord? target = _target;
            _target = null;
            return target is { } resolution && resolution.Contains(pitchClass) ? resolution : _chords.Choose(pitchClass, _key, plain);
        }

        _foreignNotes++;
        if (_foreignNotes == 1 && SecondaryDominant(pitchClass) is { } dominant)
        {
            return dominant;
        }

        return Modulate(pitchClass);
    }

    public Chord? Advance(double nowMs, int soundingPitchClass, bool notePending)
    {
        if (_cadence != CadenceStep.None)
        {
            return AdvanceCadence(nowMs);
        }

        if (!_looping)
        {
            return null;
        }

        if (!_loopAnchored)
        {
            // The tonic chord started now; the loop changes on every other beat, the first at least one beat later.
            _loopAnchored = true;
            _nextLoopMs = Math.Ceiling((nowMs + _beatMs) / (2 * _beatMs)) * 2 * _beatMs;
            return null;
        }

        // A new note waiting out its hold time may stop the loop, so the change waits for it.
        if (nowMs < _nextLoopMs || notePending)
        {
            return null;
        }

        _loopIndex = (_loopIndex + 1) % LoopDegrees.Length;
        _nextLoopMs += 2 * _beatMs;
        return LoopChord(soundingPitchClass);
    }

    // The chord the loop is on; V7 takes a sounding tonic as its 11th.
    private Chord LoopChord(int soundingPitchClass)
    {
        int degree = LoopDegrees[_loopIndex];
        Chord chord = On(_loopKey, degree);
        return degree == 5 && soundingPitchClass == _loopKey.Tonic ? chord with { Tension = Eleventh } : chord;
    }

    private Chord? AdvanceCadence(double nowMs)
    {
        if (double.IsNaN(_cadenceAtMs))
        {
            // II came now; V comes on the next beat of the grid.
            _cadenceAtMs = (Math.Floor(nowMs / _beatMs) + 1) * _beatMs;
        }

        if (nowMs < _cadenceAtMs)
        {
            return null;
        }

        if (_cadence == CadenceStep.Dominant)
        {
            _cadence = CadenceStep.Tonic;
            _cadenceAtMs += _beatMs;
            return _cadenceDominant;
        }

        _cadence = CadenceStep.None;
        return _cadenceTonic;
    }

    // The V7 of a diatonic chord other than the tonic and the m7b5 chord that contains the note, if any.
    private Chord? SecondaryDominant(int pitchClass)
    {
        foreach (int degree in SecondaryTargets)
        {
            Chord target = On(_key, degree);
            Chord dominant = new((target.Root + 7) % 12, ChordQuality.Dominant7);
            if (target.Quality != ChordQuality.HalfDiminished7 && dominant.Contains(pitchClass) && _chords.DegreeOf(_key, dominant) == 0)
            {
                _target = target;
                return dominant;
            }
        }

        return null;
    }

    // Moves to the nearest key with the note and starts its II – V – I: II now, V on the next beat, I on the one after.
    private Chord Modulate(int pitchClass)
    {
        _key = CircleOfFifths.NearestKeyWith(new CircleOfFifths.InScale(), _key, pitchClass);
        _moved = true;
        _foreignNotes = 0;
        _target = null;
        _cadence = CadenceStep.Dominant;
        _cadenceAtMs = double.NaN;
        _cadenceDominant = On(_key, 5);
        _cadenceTonic = On(_key, 1);
        return On(_key, 2);
    }

    private Chord On(Key key, int degree)
    {
        return _chords.ChordOn(key, degree) ?? throw new InvalidOperationException("Jazz has a chord on every degree.");
    }

    /// <summary>
    /// The diatonic seventh chords with the default priority and the common successors. Major: Imaj7 IIm7 IIIm7 IVmaj7
    /// V7 VIm7 VIIm7b5. Minor: natural minor with the harmonic-minor V7, Im7 IIm7b5 bIIImaj7 IVm7 V7 bVImaj7 bVII7.
    /// </summary>
    private sealed class JazzSevenths() : ScaleRules(DefaultPriority, (key, degree) => SeventhOn(key, degree))
    {
        private static readonly ChordQuality[] Major =
            [ChordQuality.Major7, ChordQuality.Minor7, ChordQuality.Minor7, ChordQuality.Major7, ChordQuality.Dominant7, ChordQuality.Minor7, ChordQuality.HalfDiminished7];

        private static readonly ChordQuality[] Minor =
            [ChordQuality.Minor7, ChordQuality.HalfDiminished7, ChordQuality.Major7, ChordQuality.Minor7, ChordQuality.Dominant7, ChordQuality.Major7, ChordQuality.Dominant7];

        // A minor key's V7 is the harmonic-minor dominant: its major third is the raised seventh degree.
        private static Chord SeventhOn(Key key, int degree)
        {
            return new Chord(key.PitchClassOf(degree), (key.IsMajor ? Major : Minor)[degree - 1]);
        }
    }
}
