namespace SolidBackup.Cli;

/// <summary>Says when a display is due to be drawn again: once per <paramref name="interval"/> on the clock <paramref name="now"/>.</summary>
public sealed class RedrawPacer(TimeSpan interval, Func<TimeSpan> now)
{
    private TimeSpan _last = now();

    /// <summary>True, once, when an interval has passed since the last time it was true (or since it was made).</summary>
    public bool Due()
    {
        TimeSpan current = now();
        if (current - _last < interval)
        {
            return false;
        }

        _last = current;
        return true;
    }
}
