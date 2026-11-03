using SolidBackup.Analysis;
using SolidBackup.Synthesis;

namespace SolidBackup.Accompaniment;

/// <summary>
/// Autoplay (<c>docs/rhythm.md</c>): once there is a chord, silence neither stops nor silences it. On a buffer in which
/// no note reaches the harmony, the chord keeps sounding, so the pattern goes on on the grid, at the strength and
/// shape the last note had at its strongest (a note fades before the gate closes, so its last buffer is quieter than
/// the note), and it is not struck anew. The gate and the end of the phrase no longer silence it. It is not a level
/// feature: it runs after them, on what they decided.
/// </summary>
public sealed class Autoplay
{
    private const int NoNote = -1;

    private int _note = NoNote;
    private double _strength;
    private ChordShape _shape = ChordShape.Whole;

    /// <param name="reachedHarmony">The estimate as it reached the harmony (past the gate).</param>
    public void Apply(in PitchEstimate reachedHarmony, ref AccompanimentFrame frame)
    {
        int note = reachedHarmony.HasNote ? reachedHarmony.MidiNote : NoNote;
        if (note != NoNote)
        {
            if (note != _note || frame.Strength >= _strength)
            {
                _strength = frame.Strength;
                _shape = frame.Shape;
            }

            _note = note;
            return;
        }

        _note = NoNote;
        if (frame.Chord is null)
        {
            return;
        }

        frame.Sounding = true;
        frame.Strike = false;
        frame.Strength = _strength;
        frame.Shape = _shape;
    }
}
