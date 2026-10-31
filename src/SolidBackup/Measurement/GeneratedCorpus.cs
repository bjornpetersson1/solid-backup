using SolidBackup.Harmony;

namespace SolidBackup.Measurement;

/// <summary>
/// The generated half of the corpus: notes on every semitone of the range as sines and sawtooths,
/// the same with white noise, glides across the range, and unvoiced silence and noise; and, for key
/// detection, melodies in known keys. Built from fixed parameters and seeds, so every call gives the same samples.
/// </summary>
public static class GeneratedCorpus
{
    public const double NoteMs = 300;
    public const double GapMs = 100;
    public const double GlideMs = 4000;
    public const float Amplitude = 0.5f;

    /// <summary>One beat of a melody; a note lasts its beats less <see cref="NoteGapMs"/>, then a short silence.</summary>
    public const double BeatMs = 450;
    public const double NoteGapMs = 50;

    // The melodies' phrases, as semitones from the tonic and beats. Each uses the scale's fourth and seventh
    // degrees, so the key is not one of its neighbours; the minor ones use the raised seventh.
    private static readonly (int Offset, double Beats)[] MajorFromTheThird =
        [(4, 1), (5, 1), (7, 1), (9, 1), (7, 2), (5, 1), (4, 1), (2, 1), (-1, 1), (0, 2), (4, 1), (2, 1), (0, 1), (-1, 1), (0, 1), (2, 1), (4, 1), (5, 1), (4, 1), (2, 1), (0, 3)];

    private static readonly (int Offset, double Beats)[] MajorFromBelow =
        [(-5, 1), (0, 1), (2, 1), (4, 2), (2, 1), (0, 1), (-1, 1), (0, 1), (2, 2), (5, 1), (4, 1), (2, 1), (-1, 1), (0, 2), (7, 1), (5, 1), (4, 1), (2, 1), (0, 3)];

    // With a chromatic passing note, the raised fourth, between the fourth and the fifth.
    private static readonly (int Offset, double Beats)[] MajorWithPassingNote =
        [(0, 1), (2, 1), (4, 1), (5, 1), (6, 0.5), (7, 1.5), (9, 1), (7, 1), (5, 1), (4, 1), (2, 1), (-1, 1), (0, 2), (4, 1), (7, 1), (5, 1), (2, 1), (-1, 1), (0, 3)];

    private static readonly (int Offset, double Beats)[] MajorFromTheFifth =
        [(7, 1), (4, 1), (5, 1), (7, 1), (9, 1), (7, 1), (5, 1), (4, 1), (2, 2), (4, 1), (0, 1), (2, 1), (-1, 1), (0, 2), (5, 1), (4, 1), (2, 1), (0, 3)];

    private static readonly (int Offset, double Beats)[] MinorFromTheTonic =
        [(0, 1), (3, 1), (7, 1), (3, 1), (2, 1), (0, 1), (-1, 1), (0, 2), (3, 1), (5, 1), (7, 1), (8, 1), (7, 2), (5, 1), (3, 1), (2, 1), (-1, 1), (0, 3)];

    private static readonly (int Offset, double Beats)[] MinorFromTheFifth =
        [(7, 1), (8, 1), (7, 1), (5, 1), (3, 1), (2, 1), (0, 2), (2, 1), (3, 1), (-1, 1), (0, 1), (3, 1), (2, 1), (-1, 1), (0, 3)];

    private static readonly (int Offset, double Beats)[] MinorFromTheThird =
        [(3, 1), (2, 1), (0, 1), (-1, 1), (0, 1), (3, 1), (7, 2), (5, 1), (3, 1), (2, 1), (5, 1), (3, 1), (2, 1), (-1, 1), (0, 3)];

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

    /// <summary>
    /// Melodies for key detection, each phrase played twice: four in major and three in minor, and two that
    /// modulate (C major → G major, A minor → D minor), with a reference key per stretch.
    /// </summary>
    public static IReadOnlyList<CorpusClip> Melodies(int sampleRate)
    {
        return
        [
            Melody(sampleRate, 11, "generated-melody-c-major", (Major(0), MajorFromTheThird)),
            Melody(sampleRate, 12, "generated-melody-g-major", (Major(7), MajorFromBelow)),
            Melody(sampleRate, 13, "generated-melody-eb-major", (Major(3), MajorWithPassingNote)),
            Melody(sampleRate, 14, "generated-melody-f-sharp-major", (Major(6), MajorFromTheFifth)),
            Melody(sampleRate, 15, "generated-melody-a-minor", (Minor(9), MinorFromTheTonic)),
            Melody(sampleRate, 16, "generated-melody-e-minor", (Minor(4), MinorFromTheFifth)),
            Melody(sampleRate, 17, "generated-melody-c-minor", (Minor(0), MinorFromTheThird)),
            Melody(sampleRate, 18, "generated-melody-c-to-g-major", (Major(0), MajorFromTheThird), (Major(7), MajorFromBelow)),
            Melody(sampleRate, 19, "generated-melody-a-to-d-minor", (Minor(9), MinorFromTheTonic), (Minor(2), MinorFromTheFifth)),
        ];
    }

    private static Key Major(int tonic)
    {
        return new Key(tonic, Mode.Ionian);
    }

    private static Key Minor(int tonic)
    {
        return new Key(tonic, Mode.Aeolian);
    }

    private static CorpusClip Melody(int sampleRate, uint seed, string name, params (Key Key, (int Offset, double Beats)[] Phrase)[] parts)
    {
        ClipBuilder builder = new ClipBuilder(sampleRate, seed).Silence(GapMs);
        foreach ((Key key, (int Offset, double Beats)[] phrase) in parts)
        {
            // Tonics from F#3 to F4, so every melody stays in a comfortable singing range.
            int tonicMidi = 54 + PitchClass.Interval(6, key.Tonic);
            builder.InKey(key);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                foreach ((int offset, double beats) in phrase)
                {
                    builder.Note(Waveform.Sine, tonicMidi + offset, (beats * BeatMs) - NoteGapMs, Amplitude).Silence(NoteGapMs);
                }
            }
        }

        return builder.Build(name);
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
