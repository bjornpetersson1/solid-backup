using SolidBackup.Analysis;

namespace SolidBackup.Harmony;

/// <summary>
/// Estimates the tonic and major or minor from the recent notes (E4-3). Every new note adds one to its pitch
/// class in a histogram that decays exponentially while notes sound; the key is the best correlation with the
/// Krumhansl–Kessler profiles over the 24 major and minor keys. A key is reported once <c>KeyFirstSeconds</c>
/// of notes have been heard and it has led every other key by <see cref="Margin"/> for the hold time; a new key
/// replaces it only after leading it by the margin for the hold time. The decay and the hold time scale with
/// <c>KeyChangeSeconds</c>. Allocates nothing per estimate.
/// </summary>
public sealed class KeyEstimator
{
    /// <summary>How far, in correlation, a key must lead before it is reported or replaces the current one.</summary>
    public const double Margin = 0.12;

    /// <summary>The histogram's time constant, as a share of <c>KeyChangeSeconds</c>.</summary>
    public const double DecayShare = 0.7;

    /// <summary>How long a key must keep its lead, in sung time, as a share of <c>KeyChangeSeconds</c>.</summary>
    public const double HoldShare = 0.15;

    /// <summary>
    /// How long a new pitch class must sound, in total since the current note last sounded, before it counts as a
    /// note and becomes the current one; so a glide or vibrato across a semitone neither counts nor interrupts the
    /// note it leaves, and a new note with vibrato still counts.
    /// </summary>
    public const double MinNoteMs = 80;

    // Krumhansl and Kessler (1982), from C.
    private static readonly double[] MajorProfile = [6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88];
    private static readonly double[] MinorProfile = [6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17];

    // Index 0–11: major on that tonic; 12–23: minor.
    private static readonly double[][] CenteredProfiles = BuildProfiles();

    private readonly double _decayMs;
    private readonly double _holdMs;
    private readonly double _firstMs;
    private readonly double[] _histogram = new double[12];
    private readonly double[] _centered = new double[12];
    private readonly double[] _correlations = new double[24];

    private double _lastNoteMs = double.NaN;
    private readonly double[] _dwellMs = new double[12];

    private int _notePitchClass = -1;
    private double _sungMs;
    private int _current = -1;
    private int _leader = -1;
    private double _leaderSinceMs;

    public KeyEstimator(double keyChangeSeconds, double keyFirstSeconds)
    {
        _decayMs = keyChangeSeconds * 1000 * DecayShare;
        _holdMs = keyChangeSeconds * 1000 * HoldShare;
        _firstMs = keyFirstSeconds * 1000;
    }

    /// <summary>The estimated key, Ionian or Aeolian, or null until the estimator is sure.</summary>
    public Key? Key => _current < 0 ? null : KeyOf(_current);

    /// <summary>Takes the next estimate; true when the key changed, including the first key.</summary>
    public bool Process(PitchEstimate estimate)
    {
        if (!estimate.HasNote)
        {
            _lastNoteMs = double.NaN;
            _notePitchClass = -1;
            Array.Clear(_dwellMs);
            return false;
        }

        double now = estimate.TimeMs;
        double elapsed = double.IsNaN(_lastNoteMs) ? 0 : now - _lastNoteMs;
        _lastNoteMs = now;
        Accumulate(estimate.MidiNote % 12, elapsed);
        int best = Correlate();
        bool leads = _current < 0 ? LeadsEveryKey(best) : best != _current && LeadsBy(best, _current);
        return leads && _sungMs >= _firstMs ? HoldLead(best) : ClearLead();
    }

    private void Accumulate(int pitchClass, double elapsed)
    {
        double decay = Math.Exp(-elapsed / _decayMs);
        for (int i = 0; i < 12; i++)
        {
            _histogram[i] *= decay;
        }

        _sungMs += elapsed;
        if (pitchClass == _notePitchClass)
        {
            Array.Clear(_dwellMs);
            return;
        }

        // Each note counts once, however long it is held: duration weighting let long notes on the fourth or
        // sixth degree pull the estimate to a neighbouring key (see specs/E4-3-key-detection.md).
        _dwellMs[pitchClass] += elapsed;
        if (_dwellMs[pitchClass] >= MinNoteMs)
        {
            _histogram[pitchClass] += 1;
            _notePitchClass = pitchClass;
            Array.Clear(_dwellMs);
        }
    }

    // Fills _correlations and returns the best key.
    private int Correlate()
    {
        double mean = 0;
        foreach (double count in _histogram)
        {
            mean += count / 12;
        }

        double energy = 0;
        for (int i = 0; i < 12; i++)
        {
            _centered[i] = _histogram[i] - mean;
            energy += _centered[i] * _centered[i];
        }

        double length = Math.Sqrt(energy);
        int best = 0;
        for (int key = 0; key < 24; key++)
        {
            double[] profile = CenteredProfiles[key];
            double product = 0;
            for (int i = 0; i < 12; i++)
            {
                product += _centered[i] * profile[i];
            }

            _correlations[key] = length > 0 ? product / length : 0;
            if (_correlations[key] > _correlations[best])
            {
                best = key;
            }
        }

        return best;
    }

    private bool LeadsEveryKey(int best)
    {
        for (int key = 0; key < 24; key++)
        {
            if (key != best && !LeadsBy(best, key))
            {
                return false;
            }
        }

        return true;
    }

    private bool LeadsBy(int key, int other)
    {
        return _correlations[key] - _correlations[other] >= Margin;
    }

    // Measured in sung time, like the histogram, so a pause cannot stand in for the hold.
    private bool HoldLead(int best)
    {
        if (best != _leader)
        {
            _leader = best;
            _leaderSinceMs = _sungMs;
        }

        if (_sungMs - _leaderSinceMs < _holdMs)
        {
            return false;
        }

        _current = best;
        _leader = -1;
        return true;
    }

    private bool ClearLead()
    {
        _leader = -1;
        return false;
    }

    private static Key KeyOf(int index)
    {
        return new Key(index % 12, index < 12 ? Mode.Ionian : Mode.Aeolian);
    }

    // Each profile rotated to its tonic, centred and scaled to unit length, so its dot product with a centred
    // histogram, divided by the histogram's length, is Pearson's correlation.
    private static double[][] BuildProfiles()
    {
        double[][] profiles = new double[24][];
        for (int key = 0; key < 24; key++)
        {
            double[] source = key < 12 ? MajorProfile : MinorProfile;
            double mean = source.Average();
            double[] rotated = [.. Enumerable.Range(0, 12).Select(i => source[PitchClass.Interval(key % 12, i)] - mean)];
            double length = Math.Sqrt(rotated.Sum(value => value * value));
            profiles[key] = [.. rotated.Select(value => value / length)];
        }

        return profiles;
    }
}
