namespace SolidBackup.Analysis;

/// <summary>
/// An in-place radix-2 complex FFT of a fixed power-of-two size. The twiddle factors (stored stage by
/// stage, so each stage reads them in order) and the bit-reversal table are built in the constructor,
/// so a transform allocates nothing.
/// </summary>
public sealed class Fft
{
    private readonly double[] _cos;
    private readonly double[] _sin;
    private readonly int[] _reversed;

    public Fft(int size)
    {
        if (!int.IsPow2(size))
        {
            throw new ArgumentException($"The FFT size {size} is not a power of two.", nameof(size));
        }

        Size = size;

        // The stage with butterflies of half-length h uses the h twiddles e^(−πi·k/h), stored from index h.
        _cos = new double[size];
        _sin = new double[size];
        for (int half = 1; half < size; half *= 2)
        {
            for (int k = 0; k < half; k++)
            {
                _cos[half + k] = Math.Cos(Math.PI * k / half);
                _sin[half + k] = Math.Sin(Math.PI * k / half);
            }
        }

        int bits = int.Log2(size);
        _reversed = new int[size];
        for (int i = 0; i < size; i++)
        {
            _reversed[i] = ReverseBits(i, bits);
        }
    }

    public int Size { get; }

    /// <summary>
    /// Transforms in place: e^(−2πi·jk/N) forward, e^(+2πi·jk/N) inverse. The inverse is not scaled; the
    /// caller multiplies by 1/N where it needs to, often folded into work it does anyway.
    /// </summary>
    public void Transform(double[] real, double[] imaginary, bool inverse)
    {
        if (real.Length != Size || imaginary.Length != Size)
        {
            throw new ArgumentException($"The FFT needs {Size} values in each part.");
        }

        Reorder(real, imaginary);
        double direction = inverse ? 1 : -1;
        for (int half = 1; half < Size; half *= 2)
        {
            for (int start = 0; start < Size; start += 2 * half)
            {
                for (int k = 0; k < half; k++)
                {
                    double cos = _cos[half + k];
                    double sin = direction * _sin[half + k];
                    int even = start + k;
                    int odd = even + half;
                    double oddReal = (real[odd] * cos) - (imaginary[odd] * sin);
                    double oddImaginary = (real[odd] * sin) + (imaginary[odd] * cos);
                    real[odd] = real[even] - oddReal;
                    imaginary[odd] = imaginary[even] - oddImaginary;
                    real[even] += oddReal;
                    imaginary[even] += oddImaginary;
                }
            }
        }
    }

    private void Reorder(double[] real, double[] imaginary)
    {
        for (int i = 0; i < Size; i++)
        {
            int j = _reversed[i];
            if (j > i)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }
        }
    }

    private static int ReverseBits(int value, int bits)
    {
        int reversed = 0;
        for (int bit = 0; bit < bits; bit++)
        {
            reversed = (reversed << 1) | ((value >> bit) & 1);
        }

        return reversed;
    }
}
