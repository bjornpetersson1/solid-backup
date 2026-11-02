using SolidBackup.Analysis;

namespace SolidBackup.Tests.Synthesis;

/// <summary>A Hann-windowed, zero-padded magnitude spectrum of rendered audio, for finding tones in it.</summary>
internal sealed class Spectrum
{
    private readonly double[] _magnitudes;
    private readonly int _sampleRate;
    private readonly int _size;

    public Spectrum(ReadOnlySpan<float> samples, int sampleRate, int size = 65_536)
    {
        _sampleRate = sampleRate;
        _size = size;
        double[] real = new double[size];
        double[] imaginary = new double[size];
        for (int i = 0; i < samples.Length; i++)
        {
            double window = 0.5 - (0.5 * Math.Cos(2 * Math.PI * i / (samples.Length - 1)));
            real[i] = samples[i] * window;
        }

        new Fft(size).Transform(real, imaginary, inverse: false);
        _magnitudes = new double[size / 2];
        for (int bin = 0; bin < _magnitudes.Length; bin++)
        {
            _magnitudes[bin] = Math.Sqrt((real[bin] * real[bin]) + (imaginary[bin] * imaginary[bin]));
        }
    }

    public double BinHz => (double)_sampleRate / _size;

    /// <summary>The strongest peak within ±<paramref name="searchCents"/> of <paramref name="hz"/>, interpolated, and its magnitude.</summary>
    public (double Hz, double Magnitude) PeakNear(double hz, double searchCents = 30)
    {
        int low = (int)(hz * Math.Pow(2, -searchCents / 1200) / BinHz);
        int high = (int)Math.Ceiling(hz * Math.Pow(2, searchCents / 1200) / BinHz);
        int best = low;
        for (int bin = low; bin <= high; bin++)
        {
            if (_magnitudes[bin] > _magnitudes[best])
            {
                best = bin;
            }
        }

        double offset = Pitch.ParabolaOffset(
            Math.Log(_magnitudes[best - 1]), Math.Log(_magnitudes[best]), Math.Log(_magnitudes[best + 1]), minimum: false);
        return ((best + offset) * BinHz, _magnitudes[best]);
    }

    /// <summary>The energy (sum of squared magnitudes) between two frequencies.</summary>
    public double Energy(double fromHz, double toHz)
    {
        double energy = 0;
        for (int bin = (int)(fromHz / BinHz); bin < Math.Min(_magnitudes.Length, (int)(toHz / BinHz)); bin++)
        {
            energy += _magnitudes[bin] * _magnitudes[bin];
        }

        return energy;
    }
}
