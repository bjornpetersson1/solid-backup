using SolidBackup.Configuration;

namespace SolidBackup.Rhythm;

/// <summary>
/// The tempo grid from <c>docs/rhythm.md</c>: sample 0 is beat 1 of bar 1, a beat lasts
/// <c>SampleRate × 60 / Bpm</c> samples (not necessarily whole), and step <i>n</i> starts at its exact time rounded to
/// the nearest sample. Steps are never built by adding rounded lengths, so the grid does not drift. Pure; allocates nothing.
/// </summary>
public sealed class TempoGrid(int sampleRate, int bpm, int beatsPerBar)
{
    /// <summary>The grid of validated settings' tempo and time signature.</summary>
    /// <exception cref="ArgumentException">The time signature is not valid; validate the settings first.</exception>
    public static TempoGrid FromSettings(AppSettings settings)
    {
        return RhythmSettings.TryParseTimeSignature(settings.TimeSignature, out int beatsPerBar)
            ? new TempoGrid(settings.SampleRate, settings.Bpm, beatsPerBar)
            : throw new ArgumentException("The time signature is not valid; validate the settings first.", nameof(settings));
    }

    public int SampleRate => sampleRate;

    public int BeatsPerBar => beatsPerBar;

    public double SamplesPerBeat { get; } = sampleRate * 60.0 / bpm;

    /// <summary>A beat's length in ms; beat <i>n</i> starts at <i>n</i> × this.</summary>
    public double BeatMs { get; } = 60_000.0 / bpm;

    /// <summary>The sample step <paramref name="step"/> starts on, with <paramref name="stepsPerBeat"/> steps per beat.</summary>
    public long StepSample(long step, int stepsPerBeat)
    {
        return (long)Math.Round(step * SamplesPerBeat / stepsPerBeat, MidpointRounding.AwayFromZero);
    }

    /// <summary>The first step that starts at or after <paramref name="sample"/>.</summary>
    public long FirstStepAtOrAfter(long sample, int stepsPerBeat)
    {
        long step = (long)Math.Floor(sample * stepsPerBeat / SamplesPerBeat);
        while (step > 0 && StepSample(step - 1, stepsPerBeat) >= sample)
        {
            step--;
        }

        while (StepSample(step, stepsPerBeat) < sample)
        {
            step++;
        }

        return step;
    }

    /// <summary>The step's place in its bar, from 0; step 0 of a bar is on the downbeat.</summary>
    public int StepInBar(long step, int stepsPerBeat)
    {
        return (int)(step % (beatsPerBar * stepsPerBeat));
    }

    /// <summary>The steps that start in [<paramref name="start"/>, <paramref name="end"/>), each with its offset from <paramref name="start"/>; allocates nothing.</summary>
    public StepEnumerator StepsIn(long start, long end, int stepsPerBeat)
    {
        return new StepEnumerator(this, start, end, stepsPerBeat);
    }

    /// <summary>A step and where it starts, as an offset from the start of the span asked for.</summary>
    public readonly record struct Step(long Index, int Offset);

    public struct StepEnumerator(TempoGrid grid, long start, long end, int stepsPerBeat)
    {
        private long _next = grid.FirstStepAtOrAfter(start, stepsPerBeat);

        public Step Current { get; private set; }

        public readonly StepEnumerator GetEnumerator()
        {
            return this;
        }

        public bool MoveNext()
        {
            long sample = grid.StepSample(_next, stepsPerBeat);
            if (sample >= end)
            {
                return false;
            }

            Current = new Step(_next, (int)(sample - start));
            _next++;
            return true;
        }
    }
}
