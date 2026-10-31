namespace SolidBackup.Analysis;

/// <summary>
/// The RMS level in dBFS over the last <see cref="WindowMs"/> of input, updated after every buffer; until
/// that much input has arrived, over all of it, so the level starts at the input's own level rather than
/// rising from nothing. Digital silence reads <see cref="FloorDbfs"/> rather than −∞. Pure and allocation-free per buffer.
/// </summary>
public sealed class LevelMeter
{
    public const double WindowMs = 10;
    public const double FloorDbfs = -120;

    private readonly double[] _squares;
    private int _next;
    private int _filled;
    private double _sum;

    public LevelMeter(int sampleRate)
    {
        _squares = new double[Duration.Samples(WindowMs, sampleRate)];
    }

    /// <summary>The number of samples the level is measured over; the level refers to the middle of them.</summary>
    public int WindowLength => _squares.Length;

    public double Process(ReadOnlySpan<float> buffer)
    {
        foreach (float sample in buffer)
        {
            double square = (double)sample * sample;
            _sum += square - _squares[_next];
            _squares[_next] = square;
            _next = _next + 1 == _squares.Length ? 0 : _next + 1;
            if (_next == 0)
            {
                // Once per window, start the running sum afresh so rounding errors never build up.
                _sum = Sum(_squares);
            }
        }

        _filled = Math.Min(_squares.Length, _filled + buffer.Length);
        if (_filled == 0)
        {
            return FloorDbfs;
        }

        // Within a window the running sum can still drift a hair below zero; it is only ever a sum of squares.
        double meanSquare = Math.Max(0, _sum) / _filled;
        return meanSquare > 0 ? Math.Max(FloorDbfs, 10 * Math.Log10(meanSquare)) : FloorDbfs;
    }

    private static double Sum(double[] values)
    {
        double sum = 0;
        foreach (double value in values)
        {
            sum += value;
        }

        return sum;
    }
}
