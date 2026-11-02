using SolidBackup.Analysis;

namespace SolidBackup.Accompaniment;

/// <summary>
/// What the analysis found in one buffer of <paramref name="BufferLength"/> samples ending at sample
/// <paramref name="EndSample"/> (counted from the first, so a tempo grid can place tones exactly) and at
/// <paramref name="TimeMs"/>: the level, the level smoothed for the level features, the gate, whether an onset
/// began, and the pitch.
/// </summary>
public readonly record struct AnalysisFrame(
    long EndSample, double TimeMs, int BufferLength, double LevelDbfs, double SmoothedLevelDbfs, bool GateOpen, bool Onset, PitchEstimate Estimate);
