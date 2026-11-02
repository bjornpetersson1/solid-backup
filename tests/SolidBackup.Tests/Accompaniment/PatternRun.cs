using SolidBackup.Accompaniment;
using SolidBackup.Harmony;
using SolidBackup.Synthesis;

namespace SolidBackup.Tests.Accompaniment;

/// <summary>A tone the synthesizer started or released, on its output sample.</summary>
public sealed record ToneEvent(long Sample, int Note, bool Start, double Strength);

/// <summary>Drives a rhythm pattern buffer by buffer and records every tone the synthesizer starts or releases.</summary>
internal sealed class PatternRun : IToneListener
{
    public const int BufferSize = 32;

    private readonly List<ToneEvent> _starts = [];
    private readonly List<ToneEvent> _releases = [];

    public IReadOnlyList<ToneEvent> Starts => _starts;

    public IReadOnlyList<ToneEvent> Releases => _releases;

    /// <summary>Plays <paramref name="samples"/> samples; <paramref name="chordAt"/> gives the chord sounding from a buffer's first sample, or null for silence.</summary>
    public static PatternRun Play(IRhythmPattern pattern, int sampleRate, long samples, Func<long, Chord?> chordAt, double strength = 1)
    {
        return Play(pattern, sampleRate, samples, start => chordAt(start) is { } chord ? AccompanimentFrame.Neutral(chord, sounding: true) with { Strength = strength } : AccompanimentFrame.Neutral(null, sounding: false));
    }

    /// <summary>Plays <paramref name="samples"/> samples with the frame <paramref name="frameAt"/> gives for each buffer's first sample.</summary>
    public static PatternRun Play(IRhythmPattern pattern, int sampleRate, long samples, Func<long, AccompanimentFrame> frameAt)
    {
        return Play(pattern, sampleRate, 0, samples, frameAt);
    }

    /// <summary>Plays from sample <paramref name="from"/>, as a pattern started there (the grid is absolute, so it may start late).</summary>
    public static PatternRun Play(IRhythmPattern pattern, int sampleRate, long from, long samples, Func<long, AccompanimentFrame> frameAt)
    {
        PatternRun run = new();
        Synthesizer synthesizer = new(Sound.Organ, sampleRate) { Listener = run };
        float[] buffer = new float[BufferSize];
        for (long start = from; start < samples; start += BufferSize)
        {
            AccompanimentFrame frame = frameAt(start);
            pattern.Play(AnalysisFrame.Position(start + BufferSize, BufferSize, sampleRate), frame, synthesizer);
            synthesizer.Render(buffer);
        }

        return run;
    }

    public void OnStart(long sample, int note, double strength)
    {
        _starts.Add(new ToneEvent(sample, note, Start: true, strength));
    }

    public void OnRelease(long sample, int note)
    {
        _releases.Add(new ToneEvent(sample, note, Start: false, 0));
    }
}
