using SolidBackup.Analysis;
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
    private readonly long[] _struckAt = new long[ChordVoicing.MaxShapedTones];
    private readonly int _spreadSamples = (int)Duration.Samples(SpreadMs, grid.SampleRate);
    private int _struckCount;

    public void Play(in AnalysisFrame analysis, in AccompanimentFrame frame, Synthesizer synthesizer)
    {
        if (!frame.Sounding || frame.Chord is not { } chord)
        {
            Release(synthesizer, analysis.StartSample, analysis.StartSample);
            _chord.Forget();
            return;
        }

        foreach (TempoGrid.Step beat in grid.StepsIn(analysis.StartSample, analysis.EndSample, StepsPerBeat))
        {
            // Voiced only on a beat, so a chord the harmony passes through between beats does not move the voicing.
            ReadOnlySpan<int> notes = _chord.For(chord, frame.Shape);
            double strength = frame.Strength * Accent.For(grid.StepInBar(beat.Index, StepsPerBeat));
            long beatSample = analysis.StartSample + beat.Offset;
            Release(synthesizer, analysis.StartSample, beatSample);
            for (int i = 0; i < notes.Length; i++)
            {
                synthesizer.StartTone(notes[i], strength, beat.Offset + (i * _spreadSamples));
                _struck[i] = notes[i];
                _struckAt[i] = beatSample + (i * _spreadSamples);
            }

            _struckCount = notes.Length;
        }
    }

    // Releases the struck tones at a sample, or, for a tone of the strum still waiting to start, on its own start, so none is left hanging.
    private void Release(Synthesizer synthesizer, long bufferStart, long atSample)
    {
        for (int i = 0; i < _struckCount; i++)
        {
            synthesizer.ReleaseTone(_struck[i], (int)(Math.Max(atSample, _struckAt[i]) - bufferStart));
        }

        _struckCount = 0;
    }
}
