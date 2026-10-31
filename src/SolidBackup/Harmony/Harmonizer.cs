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
public sealed class Harmonizer(IChordRules rules, Key key, HarmonyTiming timing)
{
    private bool _sounding;
    private double _silenceStartMs = double.NegativeInfinity;
    private int _notePitchClass;
    private double _noteStartMs;
    private bool _pending;
    private double _lastChangeMs;

    public Key Key { get; } = key;

    /// <summary>The chord in effect, or null before the first note.</summary>
    public Chord? Chord { get; private set; }

    /// <summary>Why validated settings cannot give a harmonizer yet, or <see cref="HarmonizerProblem.None"/>.</summary>
    public static HarmonizerProblem Check(AppSettings settings)
    {
        if (!HarmonySettings.TryParseName(settings.HarmonyStyle, out HarmonyStyle style) || !ChordRules.IsBuilt(style))
        {
            return HarmonizerProblem.StyleNotBuilt;
        }

        return settings.Key == HarmonySettings.AutoKey ? HarmonizerProblem.AutoKey : HarmonizerProblem.None;
    }

    /// <summary>Rules for the settings' style, key and mode. Validate the settings first.</summary>
    /// <exception cref="ArgumentException"><see cref="Check"/> finds a problem.</exception>
    public static Harmonizer FromSettings(AppSettings settings)
    {
        if (Check(settings) is not HarmonizerProblem.None and var problem)
        {
            throw new ArgumentException($"The settings cannot give a harmonizer: {problem}.", nameof(settings));
        }

        if (!HarmonySettings.TryParseName(settings.HarmonyStyle, out HarmonyStyle style) || !ChordRules.TryCreate(style, out IChordRules? rules)
            || !PitchClass.TryParse(settings.Key, out int tonic) || !HarmonySettings.TryParseName(settings.Mode, out Mode mode))
        {
            throw new ArgumentException("The harmony settings are not valid; validate them first.", nameof(settings));
        }

        return new Harmonizer(rules, new Key(tonic, mode), HarmonyTiming.FromSettings(settings));
    }

    /// <summary>Takes the next estimate; true when the chord changed.</summary>
    public bool Process(PitchEstimate estimate)
    {
        double now = estimate.TimeMs;
        if (!estimate.HasNote)
        {
            if (_sounding)
            {
                _sounding = false;
                _silenceStartMs = now;
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
            Chord? previous = now - _silenceStartMs >= timing.PhraseResetMs ? null : Chord;
            return Change(rules.Choose(pitchClass, Key, previous), now);
        }

        if (pitchClass != _notePitchClass)
        {
            _notePitchClass = pitchClass;
            _noteStartMs = now;
            _pending = true;
        }

        if (!_pending || now - _noteStartMs < timing.MinNoteForChangeMs || now - _lastChangeMs < timing.MinChordDurationMs)
        {
            return false;
        }

        _pending = false;
        return Change(rules.Choose(pitchClass, Key, Chord), now);
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
