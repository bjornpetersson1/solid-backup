namespace SolidBackup.Accompaniment;

/// <summary>
/// Onsets: after an onset, the next new note within <see cref="NoteWaitMs"/> is ready for the harmony at once,
/// without its hold times, and the chord it gives, new or the same, is struck anew: at once, or, while the harmony
/// waits for the note's beat (a change-rate grid), on that beat; nothing is struck if the note ends or changes first. If no new note comes in that
/// time, the current chord is struck anew when it runs out. A note is new when it differs from the note before the
/// onset's buffer, so a note the detector already reports in that buffer counts. The detector's own note hold makes
/// its notes stable, so a passing pitch does not count. An onset is not a phrase start.
/// </summary>
public sealed class OnsetFeature : ILevelFeature
{
    /// <summary>How long after an onset a new stable note may arrive; the pitch detector needs a moment after the level rises.</summary>
    public const double NoteWaitMs = 60;

    private const int NoNote = -1;

    private double _waitUntilMs = double.NegativeInfinity;
    private int _pitchClassBefore = NoNote;
    private int _pitchClassAtOnset = NoNote;
    private bool _strike;
    private int _strikeNote = NoNote;

    public void BeforeHarmony(in AnalysisFrame analysis, ref HarmonyInput input)
    {
        int pitchClass = input.Estimate.HasNote ? input.Estimate.MidiNote % 12 : NoNote;
        if (analysis.Onset)
        {
            _waitUntilMs = analysis.TimeMs + NoteWaitMs;
            _pitchClassAtOnset = _pitchClassBefore;
        }

        _pitchClassBefore = pitchClass;
        if (pitchClass != _strikeNote)
        {
            _strikeNote = NoNote;
        }

        if (double.IsNegativeInfinity(_waitUntilMs))
        {
            return;
        }

        bool newNote = pitchClass != NoNote && pitchClass != _pitchClassAtOnset;
        if (newNote)
        {
            input.Immediate = true;
            _strikeNote = pitchClass;
            _waitUntilMs = double.NegativeInfinity;
        }
        else if (analysis.TimeMs >= _waitUntilMs)
        {
            _strike = true;
            _waitUntilMs = double.NegativeInfinity;
        }
    }

    public void AfterHarmony(in AnalysisFrame analysis, ref AccompanimentFrame frame)
    {
        bool chosen = _strikeNote != NoNote && !frame.ChangeAwaitsBeat;
        frame.Strike = _strike || chosen;
        _strike = false;
        if (chosen)
        {
            _strikeNote = NoNote;
        }
    }
}
