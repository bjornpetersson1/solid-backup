namespace SolidBackup.Analysis;

internal static class Duration
{
    /// <summary>The number of samples that last at least <paramref name="milliseconds"/>.</summary>
    public static long Samples(double milliseconds, int sampleRate)
    {
        return (long)Math.Ceiling(milliseconds * sampleRate / 1000);
    }
}
