using SolidBackup.Rhythm;
using SolidBackup.Synthesis;

namespace SolidBackup.Accompaniment;

/// <summary>
/// Strum (<c>docs/rhythm.md</c>): on every beat the chord is struck downwards, the lowest tone exactly on the beat and
/// each next one <see cref="SpreadMs"/> after the one below; a strike releases the previous strike's tones. A new chord,
/// or shape, is used from the next beat; the first chord after silence waits for it too.
/// </summary>
public sealed class StrumPattern(TempoGrid grid) : IRhythmPattern
{
    public const int StepsPerBeat = 1;
    public const double SpreadMs = 10;

    private readonly ChordTones _chord = new();
    private readonly int[] _struck = new int[ChordVoicing.MaxShapedTones];
    private readonly int _spreadSamples = (int)Math.Round(SpreadMs * grid.SampleRate / 1000);
    private int _struckCount;

    public void Play(in AnalysisFrame analysis, in AccompanimentFrame frame, Synthesizer synthesizer)
    {
        if (!frame.Sounding || frame.Chord is not { } chord)
        {
            Release(synthesizer, offset: 0);
            _chord.Forget();
            return;
        }

        ReadOnlySpan<int> notes = _chord.For(chord, frame.Shape);
        foreach (TempoGrid.Step beat in grid.StepsIn(analysis.StartSample, analysis.EndSample, StepsPerBeat))
        {
            double strength = frame.Strength * Accent.For(grid.StepInBar(beat.Index, StepsPerBeat));
            Release(synthesizer, beat.Offset);
            for (int i = 0; i < notes.Length; i++)
            {
                synthesizer.StartTone(notes[i], strength, beat.Offset + (i * _spreadSamples));
            }

            notes.CopyTo(_struck);
            _struckCount = notes.Length;
        }
    }

    private void Release(Synthesizer synthesizer, int offset)
    {
        foreach (int note in _struck.AsSpan(0, _struckCount))
        {
            synthesizer.ReleaseTone(note, offset);
        }

        _struckCount = 0;
    }
}
