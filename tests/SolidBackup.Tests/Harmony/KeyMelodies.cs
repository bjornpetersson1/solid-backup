using SolidBackup.Analysis;
using SolidBackup.Harmony;
using SolidBackup.Measurement;

namespace SolidBackup.Tests.Harmony;

/// <summary>The generated melodies as pitch estimates read from their references, one per 64-frame buffer at 48 kHz.</summary>
internal static class KeyMelodies
{
    public const double StepMs = 64 * 1000.0 / 48_000;

    public static readonly IReadOnlyList<CorpusClip> Clips = GeneratedCorpus.Melodies(48_000);

    public static CorpusClip Clip(string name)
    {
        return Clips.Single(clip => clip.Name == name);
    }

    /// <summary>The estimates of a melody; <paramref name="transform"/> may change the pitch (in MIDI cents) at a time.</summary>
    public static PitchEstimate[] Estimates(CorpusClip clip, Func<double, double, double>? transform = null)
    {
        List<PitchEstimate> estimates = [];
        for (double time = 0; time < clip.Reference.DurationMs; time += StepMs)
        {
            double? pitch = clip.Reference.SegmentAt(time) is { HasPitch: true } segment ? segment.PitchAt(time) : null;
            estimates.Add(pitch is { } cents
                ? new PitchEstimate(time, true, transform?.Invoke(time, cents) ?? cents, 1)
                : PitchEstimate.NoNote(time, 0));
        }

        return [.. estimates];
    }

    /// <summary>Every key the estimator reports, with the time of the estimate that changed it.</summary>
    public static List<(double TimeMs, Key Key)> KeyChanges(PitchEstimate[] estimates, double keyChangeSeconds = 4)
    {
        KeyEstimator estimator = new(keyChangeSeconds, keyFirstSeconds: 2);
        List<(double, Key)> changes = [];
        foreach (PitchEstimate estimate in estimates)
        {
            if (estimator.Process(estimate) && estimator.Key is { } key)
            {
                changes.Add((estimate.TimeMs, key));
            }
        }

        return changes;
    }
}
