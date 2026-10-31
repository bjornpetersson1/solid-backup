using SolidBackup.Analysis;

namespace SolidBackup.Measurement;

/// <summary>
/// The offline analysis behind <see cref="ReferenceLabeler"/>: the level envelope of a recording, its
/// loud stretches, and a normalised-autocorrelation pitch search over a chosen lag range.
/// </summary>
internal sealed class RecordingAnalysis
{
    public const double HopMs = 10;

    private const double LevelWindowMs = 10;
    private const double FloorDb = -120;
    private const double ActiveAboveFloorDb = 12;
    private const double ActiveBelowPeakDb = 30;
    private const double MergeGapMs = 75;
    private const double MinNoteMs = 80;
    private const double RegionHopMs = 30;
    private const double MinCorrelation = 0.8;
    private const double StrongPeakShare = 0.9;

    private readonly float[] _samples;
    private readonly int _sampleRate;

    // Running sum of squared samples, so any window's energy is one subtraction.
    private readonly double[] _energy;

    public RecordingAnalysis(float[] samples, int sampleRate)
    {
        _samples = samples;
        _sampleRate = sampleRate;
        _energy = new double[samples.Length + 1];
        for (int i = 0; i < samples.Length; i++)
        {
            _energy[i + 1] = _energy[i] + ((double)samples[i] * samples[i]);
        }

        Level = LevelPerMs();
        if (Level.Length == 0)
        {
            throw new InvalidDataException("Inspelningen är kortare än en millisekund.");
        }

        double[] sorted = Level.Order().ToArray();
        Floor = PercentileOfSorted(sorted, 0.1);
        Peak = PercentileOfSorted(sorted, 0.99);
    }

    /// <summary>How a pitch search picks its lag among the correlation peaks.</summary>
    public enum LagChoice
    {
        /// <summary>The highest peak; safe when the range is a semitone either side of a known note.</summary>
        Best,

        /// <summary>The shortest peak close to the highest, so a note is not taken for its lower octave.</summary>
        ShortestStrong,
    }

    /// <summary>RMS level in dBFS over a 10 ms window centred on every millisecond.</summary>
    public double[] Level { get; }

    public double Floor { get; }

    public double Peak { get; }

    public double DurationMs => _samples.Length * 1000.0 / _sampleRate;

    /// <summary>
    /// The loud stretches that hold a strongly periodic sound anywhere in the corpus range, with their median
    /// pitch; loud stretches without one (a breath, a click) are left to the gaps.
    /// </summary>
    public List<(int Start, int End, double MedianCents)> PitchedRegions()
    {
        List<(int Start, int End, double MedianCents)> regions = [];
        foreach ((int start, int end) in ActiveRegions())
        {
            List<(double TimeMs, double Cents)> frames = PitchFrames(start, end, RegionHopMs, CorpusRange.LowestMidi * 100.0, CorpusRange.HighestMidi * 100.0, LagChoice.ShortestStrong);
            if (frames.Count > 0)
            {
                regions.Add((start, end, Median(frames.Select(frame => frame.Cents))));
            }
        }

        return regions;
    }

    /// <summary>The pitch in MIDI cents every <paramref name="hopMs"/> from start to end, for the frames periodic enough.</summary>
    public List<(double TimeMs, double Cents)> PitchFrames(double startMs, double endMs, double hopMs, double lowestCents, double highestCents, LagChoice choice)
    {
        int minLag = (int)Math.Floor(_sampleRate / Pitch.Frequency(highestCents));
        int maxLag = (int)Math.Ceiling(_sampleRate / Pitch.Frequency(lowestCents));
        int window = Math.Max(2 * maxLag, _sampleRate / 100);
        double[] correlation = new double[maxLag - minLag + 3];
        List<(double TimeMs, double Cents)> frames = [];
        for (double ms = startMs; ms < endMs; ms += hopMs)
        {
            int from = (int)(ms * _sampleRate / 1000) - (window / 2);
            if (from < 0 || from + window + maxLag + 1 > _samples.Length)
            {
                continue;
            }

            for (int lag = minLag - 1; lag <= maxLag + 1; lag++)
            {
                correlation[lag - minLag + 1] = Correlation(from, window, lag);
            }

            if (PickPeak(correlation, choice) is int index)
            {
                double lag = index + minLag - 1 + ParabolaOffset(correlation, index);
                frames.Add((ms, Pitch.Cents(_sampleRate / lag)));
            }
        }

        return frames;
    }

    public static double Median(IEnumerable<double> values)
    {
        return PercentileOfSorted(values.Order().ToArray(), 0.5);
    }

    private List<(int Start, int End)> ActiveRegions()
    {
        double threshold = Math.Max(Floor + ActiveAboveFloorDb, Peak - ActiveBelowPeakDb);
        List<(int Start, int End)> regions = [];
        int start = -1;
        for (int ms = 0; ms <= Level.Length; ms++)
        {
            bool active = ms < Level.Length && Level[ms] >= threshold;
            if (active && start < 0)
            {
                // A stretch that starts right after the previous one ends continues it (e.g. a note's tail).
                start = regions.Count > 0 && ms - regions[^1].End < MergeGapMs ? Pop(regions) : ms;
            }
            else if (!active && start >= 0)
            {
                regions.Add((start, ms));
                start = -1;
            }
        }

        return regions.Where(region => region.End - region.Start >= MinNoteMs).ToList();
    }

    private static int Pop(List<(int Start, int End)> regions)
    {
        int start = regions[^1].Start;
        regions.RemoveAt(regions.Count - 1);
        return start;
    }

    // The index of the chosen peak, ignoring the padding at both ends; null when no lag is periodic enough.
    private static int? PickPeak(double[] correlation, LagChoice choice)
    {
        int best = 1;
        for (int index = 2; index < correlation.Length - 1; index++)
        {
            best = correlation[index] > correlation[best] ? index : best;
        }

        if (correlation[best] < MinCorrelation)
        {
            return null;
        }

        if (choice == LagChoice.Best)
        {
            return best;
        }

        // Only a true local peak counts: a best value at the edge of the range is the slope of a peak outside it.
        for (int index = 1; index <= best; index++)
        {
            if (correlation[index] >= StrongPeakShare * correlation[best]
                && correlation[index] >= correlation[index - 1] && correlation[index] >= correlation[index + 1])
            {
                return index;
            }
        }

        return null;
    }

    private double Correlation(int from, int window, int lag)
    {
        double cross = 0;
        for (int i = from; i < from + window; i++)
        {
            cross += _samples[i] * _samples[i + lag];
        }

        double first = _energy[from + window] - _energy[from];
        double second = _energy[from + lag + window] - _energy[from + lag];
        return first > 0 && second > 0 ? cross / Math.Sqrt(first * second) : 0;
    }

    private static double ParabolaOffset(double[] values, int index)
    {
        double left = values[index - 1];
        double right = values[index + 1];
        double curvature = left - (2 * values[index]) + right;
        return curvature < 0 ? Math.Clamp(0.5 * (left - right) / curvature, -0.5, 0.5) : 0;
    }

    private double[] LevelPerMs()
    {
        int half = (int)(LevelWindowMs * _sampleRate / 2000);
        double[] level = new double[(int)(_samples.Length * 1000L / _sampleRate)];
        for (int ms = 0; ms < level.Length; ms++)
        {
            int centre = (int)((long)ms * _sampleRate / 1000);
            int from = Math.Max(0, centre - half);
            int to = Math.Min(_samples.Length, centre + half);
            double meanSquare = (_energy[to] - _energy[from]) / Math.Max(1, to - from);
            level[ms] = meanSquare > 0 ? Math.Max(FloorDb, 10 * Math.Log10(meanSquare)) : FloorDb;
        }

        return level;
    }

    private static double PercentileOfSorted(double[] sorted, double fraction)
    {
        return sorted[(int)Math.Min(sorted.Length - 1, fraction * sorted.Length)];
    }
}
