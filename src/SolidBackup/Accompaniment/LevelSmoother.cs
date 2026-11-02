namespace SolidBackup.Accompaniment;

/// <summary>Smooths a level in dB with a one-pole filter of <see cref="TimeConstantMs"/>, per buffer.</summary>
public sealed class LevelSmoother(int sampleRate)
{
    public const double TimeConstantMs = 50;

    private double _level = double.NaN;
    private int _bufferLength;
    private double _coefficient;

    public double Process(double levelDbfs, int bufferLength)
    {
        if (double.IsNaN(_level))
        {
            return Follow(levelDbfs);
        }

        if (bufferLength != _bufferLength)
        {
            _bufferLength = bufferLength;
            _coefficient = 1 - Math.Exp(-bufferLength * 1000.0 / (TimeConstantMs * sampleRate));
        }

        _level += (levelDbfs - _level) * _coefficient;
        return _level;
    }

    /// <summary>Takes the level as it is, so smoothing goes on from there.</summary>
    public double Follow(double levelDbfs)
    {
        _level = levelDbfs;
        return _level;
    }
}
