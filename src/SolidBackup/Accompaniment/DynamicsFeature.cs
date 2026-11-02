namespace SolidBackup.Accompaniment;

/// <summary>
/// Dynamics: the chord's strength follows the smoothed input level, mapped linearly in dB from <c>DynamicsQuietDbfs</c>
/// (−<see cref="RangeDb"/> dB) to <c>DynamicsLoudDbfs</c> (0 dB), clamped outside.
/// </summary>
public sealed class DynamicsFeature(double quietDbfs, double loudDbfs) : ILevelFeature
{
    /// <summary>The strength at <c>DynamicsQuietDbfs</c> and below, in dB.</summary>
    public const double RangeDb = 24;

    public void AfterHarmony(in AnalysisFrame analysis, ref AccompanimentFrame frame)
    {
        frame.Strength = Strength(analysis.SmoothedLevelDbfs);
    }

    public double Strength(double levelDbfs)
    {
        double position = Math.Clamp((levelDbfs - quietDbfs) / (loudDbfs - quietDbfs), 0, 1);
        return Math.Pow(10, (position - 1) * RangeDb / 20);
    }
}
