using SolidBackup.Analysis;

namespace SolidBackup.Accompaniment;

/// <summary>
/// What the analysis found in one buffer of <paramref name="BufferLength"/> samples ending at sample
/// <paramref name="EndSample"/> (counted from the first, so a tempo grid can place tones exactly) and at
/// <paramref name="TimeMs"/>: the level, the level smoothed for the level features, the gate, whether an onset
/// began, and the pitch.
/// </summary>
public readonly record struct AnalysisFrame(
    long EndSample, double TimeMs, int BufferLength, double LevelDbfs, double SmoothedLevelDbfs, bool GateOpen, bool Onset, PitchEstimate Estimate)
{
    public long StartSample => EndSample - BufferLength;

    /// <summary>Only a buffer's place, for playing a pattern without analysis (rendering chords, tests).</summary>
    public static AnalysisFrame Position(long endSample, int bufferLength, int sampleRate)
    {
        return new AnalysisFrame(endSample, endSample * 1000.0 / sampleRate, bufferLength, 0, 0, GateOpen: true, Onset: false, default);
    }
}
