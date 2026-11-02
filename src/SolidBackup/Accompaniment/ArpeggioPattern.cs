using SolidBackup.Harmony;
using SolidBackup.Rhythm;
using SolidBackup.Synthesis;

namespace SolidBackup.Accompaniment;

/// <summary>
/// Arpeggio (<c>docs/rhythm.md</c>): on every eighth note of the grid one chord tone starts and the previous one is
/// released. Step <i>k</i> of the bar plays tone <i>k</i> mod the number of tones, lowest first, so every bar starts on
/// the lowest tone. A new chord, or shape, is used from the next step; the first chord after silence waits for it too.
/// </summary>
public sealed class ArpeggioPattern(TempoGrid grid) : IRhythmPattern
{
    public const int StepsPerBeat = 2;

    private const int NoTone = -1;

    private readonly ChordTones _chord = new();
    private int _sounding = NoTone;

    public void Play(in AnalysisFrame analysis, in AccompanimentFrame frame, Synthesizer synthesizer)
    {
        if (!frame.Sounding || frame.Chord is not { } chord)
        {
            Stop(synthesizer);
            return;
        }

        foreach (TempoGrid.Step step in grid.StepsIn(analysis.StartSample, analysis.EndSample, StepsPerBeat))
        {
            // Voiced only on a step, so a chord the harmony passes through between steps does not move the voicing.
            ReadOnlySpan<int> notes = _chord.For(chord, frame.Shape);
            int stepInBar = grid.StepInBar(step.Index, StepsPerBeat);
            if (_sounding != NoTone)
            {
                synthesizer.ReleaseTone(_sounding, step.Offset);
            }

            _sounding = notes[stepInBar % notes.Length];
            synthesizer.StartTone(_sounding, frame.Strength * Accent.For(stepInBar), step.Offset);
        }
    }

    private void Stop(Synthesizer synthesizer)
    {
        if (_sounding != NoTone)
        {
            synthesizer.ReleaseTone(_sounding);
            _sounding = NoTone;
        }

        _chord.Forget();
    }
}
