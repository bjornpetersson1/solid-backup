namespace SolidBackup.Live;

/// <summary>
/// Live mode's latency as Measurement defines it, in milliseconds: the input stream's reported latency, the chain's
/// measured delay (<see cref="ChainDelay"/>; null when it could not be measured), one buffer for the hand-over from
/// the input's thread to the output's, and the output stream's reported latency.
/// </summary>
public readonly record struct LiveLatency(double InputMs, double? ChainMs, double HandOverMs, double OutputMs)
{
    /// <summary>The sum, or null when the chain's part is unknown.</summary>
    public double? TotalMs => ChainMs is { } chain ? InputMs + chain + HandOverMs + OutputMs : null;

    /// <summary>True when the total is known and below <paramref name="targetMs"/>.</summary>
    public bool Meets(double targetMs)
    {
        return TotalMs < targetMs;
    }
}
