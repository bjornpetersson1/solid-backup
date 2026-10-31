namespace SolidBackup.Analysis;

/// <summary>
/// Finds onsets as a sudden rise in level: the level after a buffer lies at least the threshold above
/// the lowest level of the last <see cref="RiseMs"/> before the buffer (always including the previous
/// buffer's, so buffers longer than the rise time still compare with the one before). An onset fires when that rise begins, not again
/// while it lasts, and never within the minimum interval of the previous one. Pure and allocation-free.
/// </summary>
public sealed class OnsetDetector
{
    public const double RiseMs = 20;

    private readonly int _sampleRate;
    private readonly double _thresholdDb;
    private readonly long _riseSamples;
    private readonly long _minIntervalSamples;
    private readonly int _levelDelay;

    // Recent levels with the sample position after their buffer, oldest first in a ring.
    private readonly double[] _levels;
    private readonly long[] _positions;
    private int _start;
    private int _count;

    private long _position;
    private long _lastOnset = long.MinValue / 2;
    private bool _rising;

    /// <param name="levelWindowLength">The level meter's window; the level refers to its middle, which sets the onset's time.</param>
    public OnsetDetector(int sampleRate, double thresholdDb, double minIntervalMs, int levelWindowLength)
    {
        _sampleRate = sampleRate;
        _thresholdDb = thresholdDb;
        _riseSamples = Duration.Samples(RiseMs, sampleRate);
        _minIntervalSamples = Duration.Samples(minIntervalMs, sampleRate);
        _levelDelay = levelWindowLength / 2;

        // Enough room for one level per sample over the rise time, the worst case of one-sample buffers.
        _levels = new double[_riseSamples + 1];
        _positions = new long[_riseSamples + 1];
    }

    /// <summary>Takes the level after a buffer and returns the onset's time in ms from the first sample, if one begins.</summary>
    public double? Process(double levelDbfs, int bufferLength)
    {
        _position += bufferLength;
        Forget(_position - bufferLength - _riseSamples);
        bool rising = levelDbfs - Lowest(levelDbfs) >= _thresholdDb;
        Remember(levelDbfs);

        bool onset = rising && !_rising && _position - _lastOnset >= _minIntervalSamples;
        _rising = rising;
        if (!onset)
        {
            return null;
        }

        // The rise was crossed somewhere in this buffer; with a sudden attack the level window has only just
        // taken it in, so the buffer's middle is closer to the attack than the window's middle.
        _lastOnset = _position;
        return (_position - Math.Min(_levelDelay, bufferLength / 2)) * 1000.0 / _sampleRate;
    }

    private void Forget(long before)
    {
        while (_count > 0 && _positions[_start] < before)
        {
            _start = (_start + 1) % _levels.Length;
            _count--;
        }
    }

    private double Lowest(double current)
    {
        double lowest = current;
        for (int i = 0; i < _count; i++)
        {
            lowest = Math.Min(lowest, _levels[(_start + i) % _levels.Length]);
        }

        return lowest;
    }

    private void Remember(double levelDbfs)
    {
        // Only very short buffers can fill the ring (it keeps the rise time plus the latest buffer); the oldest level goes.
        if (_count == _levels.Length)
        {
            _start = (_start + 1) % _levels.Length;
            _count--;
        }

        int index = (_start + _count) % _levels.Length;
        _levels[index] = levelDbfs;
        _positions[index] = _position;
        _count++;
    }
}
