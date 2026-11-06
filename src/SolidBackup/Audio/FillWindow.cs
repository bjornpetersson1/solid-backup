namespace SolidBackup.Audio;

/// <summary>
/// The least a ring held before a read, over a window of reads (E8-1, E8-2): a jitter buffer judges its fill by its
/// trough, since bursts of callbacks make every other reading swing by several buffers.
/// </summary>
public sealed class FillWindow(int length)
{
    private long _minimum = long.MaxValue;
    private int _filled;

    /// <summary>
    /// Notes the fill before a read of <paramref name="samples"/>; at the end of a window gives its least fill and
    /// starts the next.
    /// </summary>
    public bool Add(long fill, int samples, out long minimum)
    {
        _minimum = Math.Min(_minimum, fill);
        _filled += samples;
        minimum = _minimum;
        if (_filled < length)
        {
            return false;
        }

        Reset();
        return true;
    }

    /// <summary>Starts a new window, e.g. after the fill was moved on purpose.</summary>
    public void Reset()
    {
        _minimum = long.MaxValue;
        _filled = 0;
    }
}
