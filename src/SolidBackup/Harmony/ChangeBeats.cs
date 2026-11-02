using SolidBackup.Rhythm;

namespace SolidBackup.Harmony;

/// <summary>
/// The beats of the tempo grid a chord change may fall on while singing, counted from the grid's origin: beat 1 of
/// each bar, beats 1 and 3, or every beat (<see cref="ChordChangesPerBar"/>). Pure; allocates nothing.
/// </summary>
public sealed class ChangeBeats
{
    private readonly TempoGrid _grid;
    private readonly ChordChangesPerBar _perBar;

    private ChangeBeats(TempoGrid grid, ChordChangesPerBar perBar)
    {
        _grid = grid;
        _perBar = perBar;
    }

    /// <summary>The allowed beats on <paramref name="grid"/>, or null for <see cref="ChordChangesPerBar.Off"/>.</summary>
    public static ChangeBeats? Create(TempoGrid grid, ChordChangesPerBar perBar)
    {
        return perBar == ChordChangesPerBar.Off ? null : new ChangeBeats(grid, perBar);
    }

    /// <summary>
    /// The time of the first allowed beat at or after <paramref name="timeMs"/>, on the sample the patterns strike it
    /// (<see cref="TempoGrid.StepSample"/>).
    /// </summary>
    public double NextAtOrAfter(double timeMs)
    {
        // Rounded to the nearest sample, as the grid rounds its beats, so a time on a beat is on its sample.
        long sample = (long)Math.Round(Math.Max(timeMs, 0) * _grid.SampleRate / 1000, MidpointRounding.AwayFromZero);
        long beat = _grid.FirstStepAtOrAfter(sample, stepsPerBeat: 1);
        while (!IsAllowed(beat))
        {
            beat++;
        }

        return _grid.StepSample(beat, stepsPerBeat: 1) * 1000.0 / _grid.SampleRate;
    }

    private bool IsAllowed(long beat)
    {
        int inBar = _grid.StepInBar(beat, stepsPerBeat: 1);
        return _perBar switch
        {
            ChordChangesPerBar.One => inBar == 0,
            ChordChangesPerBar.Two => inBar is 0 or 2,
            _ => true,
        };
    }
}
