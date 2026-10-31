using System.Numerics;

using SolidBackup.Configuration;

namespace SolidBackup.Analysis;

/// <summary>
/// Monophonic YIN pitch detection, one estimate per buffer. It keeps the most recent samples as its
/// analysis window, computes YIN's difference function through an FFT, picks the first dip of the
/// cumulative mean normalised difference below 1 − the confidence threshold, and refines it with a
/// parabola. A note is reported only with such a dip, inside the configured range and above an energy floor,
/// and a change to a different note only once it has held for <see cref="NoteHold.HoldMs"/>.
/// Pure and deterministic; every buffer is allocated in the constructor.
/// </summary>
public sealed class PitchDetector
{
    /// <summary>Below this RMS level the window is taken as digital silence and gives no note.</summary>
    public const double EnergyFloorDbfs = -80;

    // Pitches this far outside the range still count, so a note sung a little flat of E2 is not lost.
    private const double RangeMarginCents = 50;

    private readonly int _sampleRate;
    private readonly double _lowestCents;
    private readonly double _highestCents;
    // YIN's absolute threshold, 1 − the confidence threshold: the first lag whose normalised difference falls
    // below it is taken, which favours the true period over its multiples (de Cheveigné and Kawahara, 2002).
    private readonly double _dipThreshold;
    private readonly int _maxLag;
    private readonly int _integration;
    private readonly double _energyFloor;
    private readonly Fft _fft;
    private readonly double[] _window;
    private readonly double[] _real;
    private readonly double[] _imaginary;
    private readonly double[] _difference;
    private readonly double[] _energy;
    private readonly NoteHold _hold;

    private long _samplesSeen;
    private int _filled;

    public PitchDetector(int sampleRate, int lowestMidi, int highestMidi, double confidenceThreshold)
    {
        _sampleRate = sampleRate;
        _lowestCents = (lowestMidi * 100.0) - RangeMarginCents;
        _highestCents = (highestMidi * 100.0) + RangeMarginCents;
        _dipThreshold = 1 - confidenceThreshold;
        _maxLag = (int)Math.Ceiling(sampleRate / Pitch.Frequency(_lowestCents)) + 1;

        // Two periods of the lowest note: the integration window is one period, plus the longest lag the
        // parabola looks at (one past the search). The FFT is the next power of two, zero-padded; since
        // every product x_j·x_{j+τ} lies inside the window, the circular correlation never wraps.
        _integration = _maxLag + 1;
        WindowLength = _integration + _maxLag + 1;
        _energyFloor = WindowLength * Math.Pow(10, EnergyFloorDbfs / 10);
        _fft = new Fft((int)BitOperations.RoundUpToPowerOf2((uint)WindowLength));
        _window = new double[WindowLength];
        _real = new double[_fft.Size];
        _imaginary = new double[_fft.Size];
        _difference = new double[_maxLag + 2];
        _energy = new double[WindowLength + 1];
        _hold = new NoteHold(sampleRate);
    }

    /// <summary>The number of samples analysed for each estimate; half of it is the detector's delay.</summary>
    public int WindowLength { get; }

    public static PitchDetector FromSettings(AppSettings settings)
    {
        if (!NoteName.TryParseMidiNumber(settings.LowestNote, out int lowest) || !NoteName.TryParseMidiNumber(settings.HighestNote, out int highest))
        {
            throw new ArgumentException("The settings' pitch range is not valid; validate them first.", nameof(settings));
        }

        return new PitchDetector(settings.SampleRate, lowest, highest, settings.PitchConfidenceThreshold);
    }

    public PitchEstimate Process(ReadOnlySpan<float> buffer)
    {
        Append(buffer);
        return _hold.Process(Estimate(), buffer.Length);
    }

    private void Append(ReadOnlySpan<float> buffer)
    {
        ReadOnlySpan<float> recent = buffer.Length > WindowLength ? buffer[^WindowLength..] : buffer;
        _window.AsSpan(recent.Length).CopyTo(_window);
        int start = WindowLength - recent.Length;
        for (int i = 0; i < recent.Length; i++)
        {
            _window[start + i] = recent[i];
        }

        _samplesSeen += buffer.Length;
        _filled = Math.Min(WindowLength, _filled + buffer.Length);
    }

    private PitchEstimate Estimate()
    {
        double timeMs = (_samplesSeen - (WindowLength / 2.0)) * 1000 / _sampleRate;
        if (_filled < WindowLength)
        {
            return PitchEstimate.NoNote(timeMs, 0);
        }

        ComputeEnergy();
        if (_energy[WindowLength] <= _energyFloor)
        {
            return PitchEstimate.NoNote(timeMs, 0);
        }

        ComputeDifference();
        (int lag, bool dip) = PickLag();
        double confidence = Math.Clamp(1 - _difference[lag], 0, 1);
        if (!dip)
        {
            return PitchEstimate.NoNote(timeMs, confidence);
        }

        // The search runs from the shortest lags, above the range too, so a tone above the range is
        // recognised as such instead of being reported as the subharmonic that falls inside it.
        double cents = Pitch.Cents(_sampleRate / RefineLag(lag));
        return cents >= _lowestCents && cents <= _highestCents
            ? new PitchEstimate(timeMs, true, cents, confidence)
            : PitchEstimate.NoNote(timeMs, confidence);
    }

    // Running sum of squares over the window, for the energy floor and the difference function.
    private void ComputeEnergy()
    {
        for (int i = 0; i < WindowLength; i++)
        {
            _energy[i + 1] = _energy[i] + (_window[i] * _window[i]);
        }
    }

    // YIN's difference d(τ) = Σ (x_j − x_{j+τ})² over the integration window, written as
    // E(0..W) + E(τ..τ+W) − 2·r(τ); the cross-correlation r comes from one complex FFT that carries the
    // integration window in its real part and the whole window in its imaginary part. The result is then
    // normalised by its cumulative mean, which is what the dip search and the confidence use.
    private void ComputeDifference()
    {
        _window.AsSpan(0, _integration).CopyTo(_real);
        _real.AsSpan(_integration).Clear();
        _window.CopyTo(_imaginary, 0);
        _imaginary.AsSpan(WindowLength).Clear();

        _fft.Transform(_real, _imaginary, inverse: false);
        CrossSpectrum();
        _fft.Transform(_real, _imaginary, inverse: true);

        double head = _energy[_integration];
        double runningSum = 0;
        _difference[0] = 1;
        for (int lag = 1; lag < _difference.Length; lag++)
        {
            double shifted = _energy[lag + _integration] - _energy[lag];
            double difference = Math.Max(0, head + shifted - (2 * _real[lag]));
            runningSum += difference;
            _difference[lag] = runningSum > 0 ? difference * lag / runningSum : 1;
        }
    }

    // With z = a + i·b, A[k] = (Z[k] + conj Z[−k]) / 2 and B[k] = (Z[k] − conj Z[−k]) / 2i; the
    // correlation Σ a_j·b_{j+τ} is the inverse transform of conj(A)·B. The halves and the inverse
    // transform's 1/N are folded into one factor.
    private void CrossSpectrum()
    {
        int n = _fft.Size;
        double scale = 0.25 / n;
        for (int k = 0; k <= n / 2; k++)
        {
            int mirror = k == 0 ? 0 : n - k;
            double zr = _real[k];
            double zi = _imaginary[k];
            double mr = _real[mirror];
            double mi = _imaginary[mirror];
            double ar = zr + mr;
            double ai = zi - mi;
            double br = zi + mi;
            double bi = mr - zr;

            // conj(A)·B
            double cr = ((ar * br) + (ai * bi)) * scale;
            double ci = ((ar * bi) - (ai * br)) * scale;
            _real[k] = cr;
            _imaginary[k] = ci;
            if (mirror != k)
            {
                // The correlation is real, so the spectrum's mirror half is the conjugate.
                _real[mirror] = cr;
                _imaginary[mirror] = -ci;
            }
        }
    }

    // The first dip below the threshold, followed down to its minimum; without one, the lowest point,
    // which gives no note but still tells how close the window came to being periodic.
    private (int Lag, bool Dip) PickLag()
    {
        int best = 2;
        for (int lag = 2; lag < _maxLag; lag++)
        {
            if (_difference[lag] < _dipThreshold)
            {
                while (lag + 1 < _maxLag && _difference[lag + 1] < _difference[lag])
                {
                    lag++;
                }

                return (lag, true);
            }

            best = _difference[lag] < _difference[best] ? lag : best;
        }

        return (best, false);
    }

    private double RefineLag(int lag)
    {
        return lag + Pitch.ParabolaOffset(_difference[lag - 1], _difference[lag], _difference[lag + 1], minimum: true);
    }
}
