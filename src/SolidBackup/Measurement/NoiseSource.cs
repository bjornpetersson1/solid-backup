namespace SolidBackup.Measurement;

/// <summary>
/// Deterministic uniform white noise from a fixed seed (xorshift32), so generated clips are the same
/// on every machine and runtime.
/// </summary>
internal sealed class NoiseSource(uint seed)
{
    // Scales the uniform noise in [-1, 1), whose RMS is 1/√3, to an RMS of 1.
    private const double UnitRmsScale = 1.7320508075688772;

    private uint _state = seed == 0 ? 1u : seed;

    /// <summary>The factor that gives <see cref="Next"/> an RMS level of <paramref name="rmsDbfs"/>.</summary>
    public static float Scale(double rmsDbfs)
    {
        return (float)(Math.Pow(10, rmsDbfs / 20) * UnitRmsScale);
    }

    public float Next()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return (float)((_state / 2147483648.0) - 1);
    }
}
