using SolidBackup.Analysis;
using SolidBackup.Configuration;

namespace SolidBackup.Harmony;

/// <summary>
/// Turns the pitch detector's estimates into the current chord, following the common rules in
/// <c>docs/harmony.md</c>: the first note after silence chooses at once; while singing, a new note is
/// evaluated once it has lasted <see cref="HarmonyTiming.MinNoteForChangeMs"/> and the chord has lasted
/// <see cref="HarmonyTiming.MinChordDurationMs"/>; silence keeps the chord, and a silence of
/// <see cref="HarmonyTiming.PhraseResetMs"/> forgets it. Time comes from the estimates, so a recording
/// gives the same chords every time. Allocates nothing per estimate.
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
    private double _lastChangeMs;
    private bool _phraseReset;

    /// <summary>A harmonizer in a fixed key.</summary>
    public Harmonizer(IChordRules rules, Key key, HarmonyTiming timing)
    {
        _rules = rules;
        _timing = timing;
        Key = key;
        _rules.Reset(key);
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

    /// <summary>The chord in effect, or null before the first note.</summary>
    public Chord? Chord { get; private set; }

    /// <summary>Whether the rules of validated settings' style are built yet.</summary>
    public static bool HasBuiltStyle(AppSettings settings)
    {
        return HarmonySettings.TryParseName(settings.HarmonyStyle, out HarmonyStyle style) && ChordRules.IsBuilt(style);
    }

    /// <summary>Rules for the settings' style, and the settings' fixed key or an estimator for <c>auto</c>. Validate the settings first.</summary>
    /// <exception cref="ArgumentException">The settings are not valid, or their style is not built yet.</exception>
    public static Harmonizer FromSettings(AppSettings settings)
    {
        if (!HarmonySettings.TryParseName(settings.HarmonyStyle, out HarmonyStyle style) || !ChordRules.TryCreate(style, out IChordRules? rules))
        {
            throw new ArgumentException($"The harmony style {settings.HarmonyStyle} is not valid or not built yet.", nameof(settings));
        }

        HarmonyTiming timing = HarmonyTiming.FromSettings(settings);
        if (settings.Key == HarmonySettings.AutoKey)
        {
            return new Harmonizer(rules, new KeyEstimator(settings.KeyChangeSeconds, settings.KeyFirstSeconds), timing);
        }

        if (!PitchClass.TryParse(settings.Key, out int tonic) || !HarmonySettings.TryParseName(settings.Mode, out Mode mode))
        {
            throw new ArgumentException("The key settings are not valid; validate them first.", nameof(settings));
        }

        return new Harmonizer(rules, new Key(tonic, mode), timing);
    }

    /// <summary>Takes the next estimate; true when the chord changed.</summary>
    public bool Process(PitchEstimate estimate)
    {
        if (_keyEstimator is not null && _keyEstimator.Process(estimate))
        {
            // A modulation re-evaluates the sounding note under the usual hold times; the first key chooses
            // at once, since nothing has been sounding for the harmony until now.
            _pending = Key is not null;
            Key = _keyEstimator.Key;
            if (Key is { } newKey)
            {
                _rules.Reset(newKey);
            }
        }

        if (Key is not { } key)
        {
            return false;
        }

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
            Chord? previous = now - _silenceStartMs >= _timing.PhraseResetMs ? null : Chord;
            _phraseReset = false;
            return Change(_rules.Choose(pitchClass, key, previous), now);
        }

        if (pitchClass != _notePitchClass)
        {
            _notePitchClass = pitchClass;
            _noteStartMs = now;
            _pending = true;
        }

        if (!_pending || now - _noteStartMs < _timing.MinNoteForChangeMs || now - _lastChangeMs < _timing.MinChordDurationMs)
        {
            return false;
        }

        _pending = false;
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
