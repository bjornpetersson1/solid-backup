using SolidBackup.Analysis;
using SolidBackup.Configuration;
using SolidBackup.Rhythm;

namespace SolidBackup.Harmony;

/// <summary>
/// Turns the pitch detector's estimates into the current chord, following the common rules in
/// <c>docs/harmony.md</c>: the first note after silence chooses at once; while singing, a new note is
/// evaluated once it has lasted <see cref="HarmonyTiming.MinNoteForChangeMs"/> and the chord has lasted
/// <see cref="HarmonyTiming.MinChordDurationMs"/>, and with <see cref="HarmonyTiming.ChangeBeats"/> the change waits for
/// the next allowed beat, if the note still sounds then; silence keeps the chord, and a silence of
/// <see cref="HarmonyTiming.PhraseResetMs"/> forgets it. With <c>auto</c> it plays nothing until its
/// <see cref="KeyEstimator"/> is sure of the key (a style without a key does not wait), and it tells the rules
/// about a key change and a phrase reset through <see cref="IChordRules.Reset"/>. Time comes from the estimates,
/// so a recording gives the same chords every time. Allocates nothing per estimate.
/// </summary>
public sealed class Harmonizer
{
    private readonly IChordRules _rules;
    private readonly KeyEstimator? _keyEstimator;
    private readonly HarmonyTiming _timing;

    private bool _sounding;
    private double _silenceStartMs = double.NegativeInfinity;
    private int _notePitchClass;
    private double _noteStartMs;
    private bool _pending;
    // When a ready note is evaluated: on its beat, or at once without a grid; null while no note is ready.
    private double? _changeAtMs;
    private double _lastChangeMs;
    private bool _phraseReset;
    private double _nowMs;
    private int _chosenPitchClass = PitchClass.None;

    /// <summary>A harmonizer in a fixed key.</summary>
    public Harmonizer(IChordRules rules, Key key, HarmonyTiming timing)
    {
        _rules = rules;
        _timing = timing;
        Key = key;
        _rules.Reset(key);
    }

    /// <summary>A harmonizer for a style without a key (Free), which ignores the key settings.</summary>
    public Harmonizer(IChordRules rules, HarmonyTiming timing)
    {
        _rules = rules;
        _timing = timing;
    }

    /// <summary>A harmonizer with <c>auto</c>: it plays no chord until <paramref name="keyEstimator"/> is sure of the key.</summary>
    public Harmonizer(IChordRules rules, KeyEstimator keyEstimator, HarmonyTiming timing)
    {
        _rules = rules;
        _keyEstimator = keyEstimator;
        _timing = timing;
    }

    /// <summary>The key in effect, or null with <c>auto</c> until the key is estimated.</summary>
    public Key? Key { get; private set; }

    /// <summary>The key the style is playing in, for display: <see cref="Key"/>, unless the style has moved it (Blues).</summary>
    public Key? CurrentKey => Key is { } key ? _rules.CurrentKey(key) : null;

    /// <summary>Whether the style needs a key; Free does not, and reports none.</summary>
    public bool UsesKey => _rules.UsesKey;

    /// <summary>The chord in effect, or null before the first note.</summary>
    public Chord? Chord { get; private set; }

    /// <summary>
    /// True while a note sounds, through a silence until it has lasted <see cref="HarmonyTiming.PhraseResetMs"/>, and
    /// for as long as the style keeps playing on its own (Jazz's loop).
    /// </summary>
    public bool InPhrase => _sounding || _nowMs - _silenceStartMs < _timing.PhraseResetMs || _rules.KeepsPlaying;

    /// <summary>Rules for the settings' style, and the settings' fixed key or an estimator for <c>auto</c>. Validate the settings first.</summary>
    /// <exception cref="ArgumentException">The settings are not valid, or their style is not built yet.</exception>
    public static Harmonizer FromSettings(AppSettings settings)
    {
        if (!SettingNames.TryParse(settings.HarmonyStyle, out HarmonyStyle style))
        {
            throw new ArgumentException($"The harmony style {settings.HarmonyStyle} is not valid.", nameof(settings));
        }

        TempoGrid grid = TempoGrid.FromSettings(settings);
        IChordRules rules = ChordRules.Create(style, grid);

        HarmonyTiming timing = HarmonyTiming.FromSettings(settings, grid);
        if (!rules.UsesKey)
        {
            return new Harmonizer(rules, timing);
        }

        if (settings.Key == HarmonySettings.AutoKey)
        {
            return new Harmonizer(rules, new KeyEstimator(settings.KeyChangeSeconds, settings.KeyFirstSeconds), timing);
        }

        if (!PitchClass.TryParse(settings.Key, out int tonic) || !SettingNames.TryParse(settings.Mode, out Mode mode))
        {
            throw new ArgumentException("The key settings are not valid; validate them first.", nameof(settings));
        }

        return new Harmonizer(rules, new Key(tonic, mode), timing);
    }

    /// <summary>True while a ready note waits for its allowed beat to be evaluated.</summary>
    public bool AwaitsBeat => _changeAtMs is not null;

    /// <summary>
    /// Takes the next estimate; true when the chord changed. With <paramref name="immediate"/> (an articulated note,
    /// E5-2's onsets), a new note is ready at once, without <see cref="HarmonyTiming.MinNoteForChangeMs"/> and
    /// <see cref="HarmonyTiming.MinChordDurationMs"/>, and is evaluated at once or on the next allowed beat.
    /// <paramref name="clockMs"/> is the time a change on a beat is weighed against (default the estimate's time); the
    /// chain gives its buffer's last sample, so a change on a beat is in effect for the buffer the pattern plays the beat in.
    /// </summary>
    public bool Process(PitchEstimate estimate, bool immediate = false, double? clockMs = null)
    {
        _nowMs = estimate.TimeMs;
        if (_keyEstimator is not null && _keyEstimator.Process(estimate))
        {
            // A modulation re-evaluates the sounding note under the usual hold times; the first key chooses
            // at once, since nothing has been sounding for the harmony until now.
            _pending |= Key is not null;
            Key = _keyEstimator.Key;
            if (Key is { } newKey)
            {
                _rules.Reset(newKey);
            }
        }

        // A style without a key (Free) goes on without one, and ignores the key it is given; the others wait for
        // auto's first key.
        Key key;
        if (Key is { } known)
        {
            key = known;
        }
        else if (!_rules.UsesKey)
        {
            key = default;
        }
        else
        {
            return false;
        }

        bool changed = ProcessNote(estimate, immediate, clockMs ?? estimate.TimeMs, key);

        // A style that keeps time (Jazz) changes on its own, without the hold times. It waits for a new note still in
        // its hold times, but not for one that is ready and waits for its beat.
        bool holding = _sounding && _pending && _notePitchClass != _chosenPitchClass && _changeAtMs is null;
        if (_rules.Advance(estimate.TimeMs, _sounding ? _notePitchClass : PitchClass.None, holding) is { } timed)
        {
            changed |= Change(timed, estimate.TimeMs);
        }

        return changed;
    }

    private bool ProcessNote(PitchEstimate estimate, bool immediate, double clockMs, Key key)
    {
        double now = estimate.TimeMs;
        if (!estimate.HasNote)
        {
            if (_sounding)
            {
                _sounding = false;
                _silenceStartMs = now;
            }

            // Reset as soon as the silence is long enough, so a style's state (Blues' key) is right during the pause.
            if (!_phraseReset && now - _silenceStartMs >= _timing.PhraseResetMs)
            {
                _phraseReset = true;
                _rules.Reset(key);
            }

            return false;
        }

        int pitchClass = estimate.MidiNote % 12;
        if (!_sounding)
        {
            _sounding = true;
            _notePitchClass = pitchClass;
            _noteStartMs = now;
            _pending = false;
            _changeAtMs = null;
            Chord? previous = now - _silenceStartMs >= _timing.PhraseResetMs ? null : Chord;
            _phraseReset = false;
            _chosenPitchClass = pitchClass;
            return Change(_rules.Choose(pitchClass, key, previous), now);
        }

        if (pitchClass != _notePitchClass)
        {
            _notePitchClass = pitchClass;
            _noteStartMs = now;
            _pending = true;
            _changeAtMs = null;
        }

        if (!_pending)
        {
            return false;
        }

        // Once the note is ready (after the hold times, or at once after an onset), it is evaluated on the next
        // allowed beat, or at once without a grid.
        bool held = now - _noteStartMs >= _timing.MinNoteForChangeMs && now - _lastChangeMs >= _timing.MinChordDurationMs;
        if (_changeAtMs is null && (held || immediate))
        {
            _changeAtMs = _timing.ChangeBeats?.NextAtOrAfter(now) ?? now;
        }

        if (_changeAtMs is not { } changeAt || clockMs < changeAt)
        {
            return false;
        }

        _pending = false;
        _changeAtMs = null;
        _chosenPitchClass = pitchClass;
        return Change(_rules.Choose(pitchClass, key, Chord), now);
    }

    private bool Change(Chord chord, double now)
    {
        if (Chord == chord)
        {
            return false;
        }

        Chord = chord;
        _lastChangeMs = now;
        return true;
    }
}
