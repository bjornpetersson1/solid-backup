using SolidBackup.Live;

namespace SolidBackup.Cli;

/// <summary>
/// While live mode monitors, checks again on each <paramref name="due"/> that the output is still safe (a headphone
/// pulled out turns the built-in output to the speakers), and stops the monitoring for the rest of the session when it
/// is not, before the microphone can hear itself.
/// </summary>
public sealed class MonitorGuard(LiveMode? mode, Func<bool> outputSafe, RedrawPacer due)
{
    public void Check()
    {
        if (mode is { Monitoring: true } && due.Due() && !outputSafe())
        {
            mode.StopMonitoring();
        }
    }
}
