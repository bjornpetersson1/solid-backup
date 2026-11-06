using SolidBackup.Analysis;

namespace SolidBackup.Tests.Analysis;

/// <summary>Simple signals for the analysis tests, at 48 kHz.</summary>
internal static class TestSignal
{
    public const int SampleRate = 48_000;

    public static float[] Sine(int midi, double amplitude, double seconds = 1, int sampleRate = SampleRate)
    {
        double frequency = Pitch.Frequency(midi * 100.0);
        return Enumerable.Range(0, (int)(sampleRate * seconds))
            .Select(i => (float)(amplitude * Math.Sin(2 * Math.PI * frequency * i / sampleRate)))
            .ToArray();
    }

    /// <summary>
    /// The largest sample-to-sample step from <paramref name="from"/> up to <paramref name="to"/>: a click or a cut
    /// shows up as a step far beyond the signal's own.
    /// </summary>
    public static double LargestStep(float[] samples, int from, int to)
    {
        double largest = 0;
        for (int i = Math.Max(1, from); i < to; i++)
        {
            largest = Math.Max(largest, Math.Abs(samples[i] - samples[i - 1]));
        }

        return largest;
    }

    public static float[] Silence(double seconds)
    {
        return new float[(int)(SampleRate * seconds)];
    }

    /// <summary>Calls <paramref name="process"/> for every whole buffer of <paramref name="samples"/>.</summary>
    public static T[] InBuffers<T>(float[] samples, int bufferLength, Func<float[], int, T> process)
    {
        List<T> results = [];
        for (int offset = 0; offset + bufferLength <= samples.Length; offset += bufferLength)
        {
            results.Add(process(samples, offset));
        }

        return [.. results];
    }
}
