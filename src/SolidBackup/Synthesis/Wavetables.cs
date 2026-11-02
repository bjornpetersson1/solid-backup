using SolidBackup.Analysis;

namespace SolidBackup.Synthesis;

/// <summary>
/// Single-cycle tables, built once with an inverse FFT and scaled to a peak of 1. The sawtooth has one table
/// per octave of MIDI notes, holding only the harmonics that stay at or below <see cref="HighestHarmonicHz"/>
/// for the octave's highest note, so no harmonic reaches 20 kHz and nothing aliases. The organ table mixes
/// harmonics 1–4 like drawbars; its fourth harmonic stays far below 20 kHz for every note the synth plays.
/// </summary>
public static class Wavetables
{
    public const int Size = 4096;
    public const double HighestHarmonicHz = 18_000;

    private const int Octaves = 11;

    private static readonly double[] OrganWeights = [1, 0.5, 0.3, 0.2];

    // One extra sample at the end repeats the first, so the lookup can interpolate without wrapping.
    private static readonly float[][] Sawtooths = BuildSawtooths();

    public static float[] Organ { get; } = Build(OrganWeights);

    public static float[] Sawtooth(int midiNote)
    {
        return Sawtooths[Math.Clamp(midiNote / 12, 0, Octaves - 1)];
    }

    /// <summary>The table's value at <paramref name="phase"/> (0 to 1), linearly interpolated.</summary>
    public static double Lookup(float[] table, double phase)
    {
        double position = phase * Size;
        int index = (int)position;
        double fraction = position - index;
        return table[index] + ((table[index + 1] - table[index]) * fraction);
    }

    private static float[][] BuildSawtooths()
    {
        float[][] tables = new float[Octaves][];
        for (int octave = 0; octave < Octaves; octave++)
        {
            double highestFundamental = Pitch.Frequency(((12 * octave) + 12) * 100);
            int harmonics = Math.Clamp((int)(HighestHarmonicHz / highestFundamental), 1, (Size / 2) - 1);

            // A sawtooth is the sum of sin(2πkx)/k.
            tables[octave] = Build([.. Enumerable.Range(1, harmonics).Select(k => 1.0 / k)]);
        }

        return tables;
    }

    // Harmonic k (from 1) with weight a: bin k of −i·a/2 and bin N−k of +i·a/2 give a·sin(2πkx).
    private static float[] Build(double[] weights)
    {
        double[] real = new double[Size];
        double[] imaginary = new double[Size];
        for (int k = 1; k <= weights.Length; k++)
        {
            imaginary[k] = -weights[k - 1] / 2;
            imaginary[Size - k] = weights[k - 1] / 2;
        }

        new Fft(Size).Transform(real, imaginary, inverse: true);
        double peak = real.Max(Math.Abs);
        float[] table = new float[Size + 1];
        for (int i = 0; i < Size; i++)
        {
            table[i] = (float)(real[i] / peak);
        }

        table[Size] = table[0];
        return table;
    }
}
