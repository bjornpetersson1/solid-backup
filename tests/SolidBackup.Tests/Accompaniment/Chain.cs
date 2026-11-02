using SolidBackup.Accompaniment;
using SolidBackup.Analysis;
using SolidBackup.Configuration;
using SolidBackup.Tests.Analysis;

namespace SolidBackup.Tests.Accompaniment;

/// <summary>One buffer of a run through the chain: the analysis, what the synthesizer was told, and its voices afterwards.</summary>
public sealed record ChainStep(AnalysisFrame Analysis, AccompanimentFrame Frame, int HeldVoices, bool Silent, long Starts);

/// <summary>Runs signals through <see cref="Accompanist"/> and records every buffer.</summary>
internal static class Chain
{
    /// <summary>Default settings in a fixed key (C Ionian, Church), with only the named features on.</summary>
    public static AppSettings Settings(params string[] features)
    {
        AppSettings settings = TestSettings.Default with { Key = "C", Mode = "Ionian", HarmonyStyle = "Church" };
        Assert.True(LevelFeatures.TrySwitchOnly(settings, features, LevelFeatures.All, out AppSettings switched));
        return switched;
    }

    public static float[] Tone(int midi, double levelDbfs, double seconds)
    {
        // A sine's RMS is its amplitude over √2.
        return TestSignal.Sine(midi, Math.Pow(10, levelDbfs / 20) * Math.Sqrt(2), seconds);
    }

    public static float[] Join(params float[][] parts)
    {
        return [.. parts.SelectMany(part => part)];
    }

    public static AnalysisFrame[] Analyze(AppSettings settings, float[] samples)
    {
        Accompanist analyser = new(settings);
        return [.. TestSignal.InBuffers(samples, settings.BufferSize, (all, offset) => analyser.Analyze(all.AsSpan(offset, settings.BufferSize)))];
    }

    public static ChainStep[] Play(AppSettings settings, AnalysisFrame[] analyses, IReadOnlyList<LevelFeatureEntry>? registry = null)
    {
        Accompanist accompanist = new(settings, registry);
        float[] buffer = new float[settings.BufferSize];
        ChainStep[] steps = new ChainStep[analyses.Length];
        for (int i = 0; i < analyses.Length; i++)
        {
            accompanist.Play(analyses[i], buffer);
            steps[i] = new ChainStep(
                analyses[i], accompanist.Frame, accompanist.Synthesizer.HeldVoices, accompanist.Synthesizer.IsSilent, accompanist.Synthesizer.Starts);
        }

        return steps;
    }

    public static ChainStep[] Run(AppSettings settings, float[] samples)
    {
        return Play(settings, Analyze(settings, samples));
    }

    /// <summary>
    /// Analyses as the detector would give them, without its gaps: each note held without a break for its length, at
    /// <paramref name="levelDbfs"/>, the gate open, and an onset on the first buffer at or after each of <paramref name="onsetsMs"/>.
    /// </summary>
    public static AnalysisFrame[] Notes(AppSettings settings, double levelDbfs, (int Midi, double Ms)[] notes, params double[] onsetsMs)
    {
        double bufferMs = settings.BufferSize * 1000.0 / settings.SampleRate;
        List<AnalysisFrame> frames = [];
        double end = 0;
        HashSet<int> onsetFrames = [.. onsetsMs.Select(onsetMs => (int)Math.Ceiling((onsetMs / bufferMs) - 1e-9))];
        foreach ((int midi, double ms) in notes)
        {
            end += ms;
            while ((frames.Count + 1) * bufferMs <= end + 1e-9)
            {
                double time = (frames.Count + 1) * bufferMs;
                PitchEstimate estimate = new(time, HasNote: true, midi * 100.0, Confidence: 1);
                long endSample = (frames.Count + 1L) * settings.BufferSize;
                frames.Add(new AnalysisFrame(endSample, time, settings.BufferSize, levelDbfs, levelDbfs, GateOpen: true, onsetFrames.Contains(frames.Count + 1), estimate));
            }
        }

        return [.. frames];
    }

    public static ChainStep At(ChainStep[] steps, double timeMs)
    {
        return steps.First(step => step.Analysis.TimeMs >= timeMs);
    }
}
