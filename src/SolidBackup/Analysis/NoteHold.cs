namespace SolidBackup.Analysis;

/// <summary>
/// The pitch detector's hysteresis. A change to a different note, or between a note and no note, is
/// passed on only once it has held for <see cref="HoldMs"/> and for at least two estimates (so a single
/// stray estimate is held back with large buffers too); until then the previous answer stands, so
/// a stray estimate (e.g. an octave off for a buffer or two) never reaches the output. An estimate within
/// a semitone of the reported pitch is the same note moving and is passed on at once.
/// </summary>
public sealed class NoteHold(int sampleRate)
{
    public const double HoldMs = 10;

    private const double SameNoteCents = 100;
    private const int MinEstimates = 2;

    private readonly long _holdSamples = (long)Math.Ceiling(HoldMs * sampleRate / 1000);

    private PitchEstimate _reported = PitchEstimate.NoNote(0, 0);
    private PitchEstimate _pending = PitchEstimate.NoNote(0, 0);
    private long _pendingSamples;
    private int _pendingEstimates;

    public PitchEstimate Process(PitchEstimate estimate, int bufferLength)
    {
        if (!SameNote(estimate, _reported))
        {
            bool continuing = SameNote(estimate, _pending);
            _pendingSamples = continuing ? _pendingSamples + bufferLength : bufferLength;
            _pendingEstimates = continuing ? _pendingEstimates + 1 : 1;
            _pending = estimate;
            if (_pendingSamples < _holdSamples || _pendingEstimates < MinEstimates)
            {
                return _reported with { TimeMs = estimate.TimeMs };
            }
        }

        _pendingSamples = 0;
        _pendingEstimates = 0;
        _reported = estimate;
        return _reported;
    }

    private static bool SameNote(PitchEstimate first, PitchEstimate second)
    {
        return first.HasNote == second.HasNote
            && (!first.HasNote || Math.Abs(first.MidiCents - second.MidiCents) < SameNoteCents);
    }
}
