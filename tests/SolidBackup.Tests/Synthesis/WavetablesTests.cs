using SolidBackup.Analysis;
using SolidBackup.Synthesis;

namespace SolidBackup.Tests.Synthesis;

public class WavetablesTests
{
    private const int SampleRate = 48_000;

    [Fact]
    public void C5Sawtooth_HasNoEnergyAbove20KhzWithin40DbOfTheFundamental()
    {
        const int c5 = 72;
        float[] table = Wavetables.Sawtooth(c5);
        double increment = Pitch.Frequency(c5 * 100) / SampleRate;
        float[] samples = new float[SampleRate];
        double phase = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)Wavetables.Lookup(table, phase);
            phase = (phase + increment) % 1;
        }

        Spectrum spectrum = new(samples, SampleRate);
        double fundamental = spectrum.PeakNear(Pitch.Frequency(c5 * 100)).Magnitude;

        Assert.True(spectrum.Energy(20_000, SampleRate / 2) < fundamental * fundamental * 1e-4);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(84)]
    public void Sawtooth_PeaksAtOne(int midiNote)
    {
        Assert.Equal(1, Wavetables.Sawtooth(midiNote).Max(Math.Abs), precision: 5);
    }

    [Fact]
    public void Organ_PeaksAtOne()
    {
        Assert.Equal(1, Wavetables.Organ.Max(Math.Abs), precision: 5);
    }
}
