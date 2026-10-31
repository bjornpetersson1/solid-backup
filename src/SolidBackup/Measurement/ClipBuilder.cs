using SolidBackup.Analysis;
using SolidBackup.Harmony;

namespace SolidBackup.Measurement;

/// <summary>Builds a generated clip piece by piece, writing its reference as it goes.</summary>
internal sealed class ClipBuilder(int sampleRate, uint seed)
{
    private const double FadeMs = 5;

    private readonly List<float> _samples = [];
    private readonly List<ReferenceSegment> _segments = [];
    private readonly List<double> _onsets = [];
    private readonly List<ReferenceKey> _keys = [];
    private readonly NoiseSource _noise = new(seed);

    private double NowMs => _samples.Count * 1000.0 / sampleRate;

    public ClipBuilder Silence(double durationMs)
    {
        double start = NowMs;
        _samples.AddRange(new float[Frames(durationMs)]);
        _segments.Add(new ReferenceSegment(start, NowMs, SegmentKind.Silence));
        return this;
    }

    /// <summary>Marks the melody as being in <paramref name="key"/> from here on.</summary>
    public ClipBuilder InKey(Key key)
    {
        _keys.Add(new ReferenceKey(NowMs, key));
        return this;
    }

    public ClipBuilder Noise(double durationMs, double rmsDbfs)
    {
        double start = NowMs;
        float scale = NoiseSource.Scale(rmsDbfs);
        int frames = Frames(durationMs);
        for (int i = 0; i < frames; i++)
        {
            _samples.Add(_noise.Next() * scale);
        }

        _segments.Add(new ReferenceSegment(start, NowMs, SegmentKind.Noise));
        return this;
    }

    public ClipBuilder Note(Waveform waveform, int midi, double durationMs, float amplitude)
    {
        return Tone(waveform, midi * 100.0, midi * 100.0, durationMs, amplitude);
    }

    public ClipBuilder Glide(int fromMidi, int toMidi, double durationMs, float amplitude)
    {
        return Tone(Waveform.Sine, fromMidi * 100.0, toMidi * 100.0, durationMs, amplitude);
    }

    /// <summary>Adds white noise over the whole clip; the silences become noise segments.</summary>
    public ClipBuilder AddNoise(double rmsDbfs)
    {
        float scale = NoiseSource.Scale(rmsDbfs);
        for (int i = 0; i < _samples.Count; i++)
        {
            _samples[i] += _noise.Next() * scale;
        }

        for (int i = 0; i < _segments.Count; i++)
        {
            if (_segments[i].Kind == SegmentKind.Silence)
            {
                _segments[i] = _segments[i] with { Kind = SegmentKind.Noise };
            }
        }

        return this;
    }

    public CorpusClip Build(string name)
    {
        return new CorpusClip(name, sampleRate, [.. _samples], new ClipReference([.. _segments], [.. _onsets]) { Keys = [.. _keys] });
    }

    private ClipBuilder Tone(Waveform waveform, double fromCents, double toCents, double durationMs, float amplitude)
    {
        double start = NowMs;
        int frames = Frames(durationMs);
        int fadeFrames = Frames(FadeMs);
        double phase = 0;
        for (int i = 0; i < frames; i++)
        {
            double cents = fromCents + ((toCents - fromCents) * i / frames);
            double frequency = Pitch.Frequency(cents);
            double value = waveform == Waveform.Sine ? Math.Sin(phase) : Sawtooth(phase, frequency);
            _samples.Add((float)(value * amplitude * Fade(i, frames, fadeFrames)));
            phase += 2 * Math.PI * frequency / sampleRate;
        }

        bool inRange = CorpusRange.ContainsPitch(fromCents) && CorpusRange.ContainsPitch(toCents);
        double[] pitches = fromCents == toCents ? [fromCents] : [fromCents, toCents];
        _segments.Add(new ReferenceSegment(start, NowMs, inRange ? SegmentKind.Note : SegmentKind.OutOfRange, pitches));
        if (inRange)
        {
            _onsets.Add(start);
        }

        return this;
    }

    // A band-limited sawtooth: the harmonics below Nyquist, summed with the Chebyshev recurrence for sin(kθ).
    private double Sawtooth(double phase, double frequency)
    {
        int harmonics = (int)((sampleRate / 2.0) / frequency);
        double twoCos = 2 * Math.Cos(phase);
        double previous = 0;
        double current = Math.Sin(phase);
        double sum = 0;
        for (int k = 1; k <= harmonics; k++)
        {
            sum += current / k;
            (previous, current) = (current, (twoCos * current) - previous);
        }

        return sum * 2 / Math.PI;
    }

    private static double Fade(int frame, int frames, int fadeFrames)
    {
        int edge = Math.Min(frame, frames - 1 - frame);
        return edge >= fadeFrames ? 1 : 0.5 - (0.5 * Math.Cos(Math.PI * edge / fadeFrames));
    }

    private int Frames(double durationMs)
    {
        return (int)Math.Round(durationMs * sampleRate / 1000);
    }
}
