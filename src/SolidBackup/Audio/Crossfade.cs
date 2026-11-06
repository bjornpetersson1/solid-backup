using SolidBackup.Analysis;

namespace SolidBackup.Audio;

/// <summary>
/// The crossfade used to splice audio without a click (E8-1, E8-2): when a stretch is dropped or repeated to keep two
/// clocks in step, the old position fades out while the new one fades in, over a few milliseconds. Fading in from or
/// out to silence is the same crossfade with silence on one side.
/// </summary>
public static class Crossfade
{
    // Long enough to be heard as smooth, short enough to stay inaudible on a voice.
    private const double LengthMs = 2;

    /// <summary>The crossfade's length in samples at a sample rate.</summary>
    public static int Length(int sampleRate)
    {
        return (int)Duration.Samples(LengthMs, sampleRate);
    }

    /// <summary>
    /// Sample <paramref name="position"/> of a crossfade of <paramref name="length"/> samples from <paramref name="from"/>
    /// (the old side) to <paramref name="to"/> (the new one).
    /// </summary>
    public static float Mix(float from, float to, int position, int length)
    {
        float weight = (position + 1f) / (length + 1f);
        return from + (weight * (to - from));
    }
}
