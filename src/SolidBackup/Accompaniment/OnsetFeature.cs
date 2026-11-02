namespace SolidBackup.Accompaniment;

/// <summary>
/// Onsets: after an onset, the next new note within <see cref="NoteWaitMs"/> is evaluated by the harmony at once,
/// without its hold times, and the chord it gives, new or the same, is struck anew. If no new note comes in that
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

    public void BeforeHarmony(in AnalysisFrame analysis, ref HarmonyInput input)
    {
        int pitchClass = input.Estimate.HasNote ? input.Estimate.MidiNote % 12 : NoNote;
        if (analysis.Onset)
        {
            _waitUntilMs = analysis.TimeMs + NoteWaitMs;
            _pitchClassAtOnset = _pitchClassBefore;
        }

        _pitchClassBefore = pitchClass;
        if (double.IsNegativeInfinity(_waitUntilMs))
        {
            return;
        }

        bool newNote = pitchClass != NoNote && pitchClass != _pitchClassAtOnset;
        if (newNote || analysis.TimeMs >= _waitUntilMs)
        {
            input.Immediate = newNote;
            _strike = true;
            _waitUntilMs = double.NegativeInfinity;
        }
    }

    public void AfterHarmony(in AnalysisFrame analysis, ref AccompanimentFrame frame)
    {
        frame.Strike = _strike;
        _strike = false;
    }
}
