using System.Globalization;

using SolidBackup.Live;

namespace SolidBackup.Cli;

/// <summary>How the console writes milliseconds and the latency's parts, for the dashboard and the summary.</summary>
public static class LatencyTexts
{
    /// <summary>Milliseconds with one decimal, e.g. "34.0".</summary>
    public static string Ms(double ms)
    {
        return ms.ToString("0.0", CultureInfo.InvariantCulture);
    }

    /// <summary>The total, or "?" when the chain's delay is unknown.</summary>
    public static string Total(LiveLatency latency)
    {
        return latency.TotalMs is { } total ? Ms(total) : "?";
    }

    /// <summary>The chain's delay, or "?" when it could not be measured.</summary>
    public static string Chain(LiveLatency latency)
    {
        return latency.ChainMs is { } chain ? Ms(chain) : "?";
    }
}
