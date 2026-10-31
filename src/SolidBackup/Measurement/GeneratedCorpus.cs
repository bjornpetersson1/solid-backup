namespace SolidBackup.Measurement;

/// <summary>
/// The generated half of the corpus: notes on every semitone of the range as sines and sawtooths,
/// the same with white noise, glides across the range, and unvoiced silence and noise.
/// Built from fixed parameters and seeds, so every call gives the same samples.
/// </summary>
public static class GeneratedCorpus
{
    public const double NoteMs = 300;
    public const double GapMs = 100;
    public const double GlideMs = 4000;
    public const float Amplitude = 0.5f;

    /// <summary>Noise level in the noisy clips: 20 dB below the sine's RMS (−9 dBFS at amplitude 0.5).</summary>
    public const double SignalToNoiseDb = 20;

    public static IReadOnlyList<CorpusClip> Create(int sampleRate)
    {
        double noiseDbfs = (20 * Math.Log10(Amplitude / Math.Sqrt(2))) - SignalToNoiseDb;
        return
        [
            Scale(sampleRate, Waveform.Sine, seed: 1).Build("generated-sine-scale"),
            Scale(sampleRate, Waveform.Sawtooth, seed: 2).Build("generated-sawtooth-scale"),
            Scale(sampleRate, Waveform.Sine, seed: 3).AddNoise(noiseDbfs).Build("generated-sine-noise-scale"),
            Scale(sampleRate, Waveform.Sawtooth, seed: 4).AddNoise(noiseDbfs).Build("generated-sawtooth-noise-scale"),
            new ClipBuilder(sampleRate, seed: 5)
                .Silence(GapMs).Glide(CorpusRange.LowestMidi, CorpusRange.HighestMidi, GlideMs, Amplitude)
                .Silence(GapMs).Glide(CorpusRange.HighestMidi, CorpusRange.LowestMidi, GlideMs, Amplitude)
                .Silence(GapMs).Build("generated-glides"),
            new ClipBuilder(sampleRate, seed: 6)
                .Silence(1000).Noise(1000, rmsDbfs: -40).Silence(1000).Noise(1000, rmsDbfs: -60)
                .Build("generated-unvoiced"),
        ];
    }

    private static ClipBuilder Scale(int sampleRate, Waveform waveform, uint seed)
    {
        ClipBuilder builder = new ClipBuilder(sampleRate, seed).Silence(GapMs);
        for (int midi = CorpusRange.LowestMidi; midi <= CorpusRange.HighestMidi; midi++)
        {
            builder.Note(waveform, midi, NoteMs, Amplitude).Silence(GapMs);
        }

        return builder;
    }
}
