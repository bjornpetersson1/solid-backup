using SolidBackup.Analysis;

namespace SolidBackup.Tests.Analysis;

/// <summary>Simple signals for the analysis tests, at 48 kHz.</summary>
internal static class TestSignal
{
    public const int SampleRate = 48_000;

    public static float[] Sine(int midi, double amplitude, double seconds = 1)
    {
        double frequency = Pitch.Frequency(midi * 100.0);
        return Enumerable.Range(0, (int)(SampleRate * seconds))
            .Select(i => (float)(amplitude * Math.Sin(2 * Math.PI * frequency * i / SampleRate)))
            .ToArray();
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
