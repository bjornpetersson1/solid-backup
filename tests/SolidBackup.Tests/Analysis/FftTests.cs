using SolidBackup.Analysis;

namespace SolidBackup.Tests.Analysis;

public sealed class FftTests
{
    [Fact]
    public void Forward_MatchesTheDirectDft()
    {
        const int size = 64;
        double[] real = Enumerable.Range(0, size).Select(i => Math.Sin(i * 0.7) + (0.3 * Math.Cos(i * 2.1))).ToArray();
        double[] imaginary = Enumerable.Range(0, size).Select(i => 0.5 * Math.Sin(i * 1.3)).ToArray();
        (double[] expectedReal, double[] expectedImaginary) = Dft(real, imaginary);

        new Fft(size).Transform(real, imaginary, inverse: false);

        for (int k = 0; k < size; k++)
        {
            Assert.Equal(expectedReal[k], real[k], 1e-9);
            Assert.Equal(expectedImaginary[k], imaginary[k], 1e-9);
        }
    }

    [Fact]
    public void Inverse_UndoesForwardUpToTheFactorN()
    {
        double[] original = Enumerable.Range(0, 256).Select(i => Math.Sin(i * 0.11) * i).ToArray();
        double[] real = [.. original];
        double[] imaginary = new double[256];
        Fft fft = new(256);

        fft.Transform(real, imaginary, inverse: false);
        fft.Transform(real, imaginary, inverse: true);

        // The inverse is unscaled, so the round trip gives N times the input.
        Assert.All(real.Zip(original), pair => Assert.Equal(pair.Second * 256, pair.First, 1e-6));
        Assert.All(imaginary, value => Assert.Equal(0, value, 1e-6));
    }

    [Fact]
    public void SizeThatIsNotAPowerOfTwo_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new Fft(1000));
    }

    private static (double[] Real, double[] Imaginary) Dft(double[] real, double[] imaginary)
    {
        int n = real.Length;
        double[] outReal = new double[n];
        double[] outImaginary = new double[n];
        for (int k = 0; k < n; k++)
        {
            for (int j = 0; j < n; j++)
            {
                double angle = -2 * Math.PI * j * k / n;
                outReal[k] += (real[j] * Math.Cos(angle)) - (imaginary[j] * Math.Sin(angle));
                outImaginary[k] += (real[j] * Math.Sin(angle)) + (imaginary[j] * Math.Cos(angle));
            }
        }

        return (outReal, outImaginary);
    }
}
