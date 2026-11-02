using SolidBackup.Accompaniment;
using SolidBackup.Configuration;
using SolidBackup.Tests.Analysis;

namespace SolidBackup.Tests.Accompaniment;

public class AccompanistTests
{
    [Fact]
    public void Process_WithEveryFeatureOn_AllocatesNothing()
    {
        AppSettings settings = Chain.Settings("gate", "onsets", "character", "dynamics");
        Accompanist accompanist = new(settings);
        float[] signal = Chain.Join(Chain.Tone(57, -36, 0.5), Chain.Tone(59, -14, 0.5), TestSignal.Silence(0.3), Chain.Tone(60, -24, 0.5));
        float[] output = new float[settings.BufferSize];
        Play(accompanist, signal, output);

        long before = GC.GetAllocatedBytesForCurrentThread();
        Play(accompanist, signal, output);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ANewFeature_RunsFromTheRegistryAndLeavesTheOthersUnchanged()
    {
        AppSettings settings = Chain.Settings("gate", "onsets", "character", "dynamics");
        AnalysisFrame[] analyses = Chain.Analyze(settings, Chain.Join(Chain.Tone(57, -30, 1), Chain.Tone(59, -16, 1)));
        CountingFeature counting = new();

        ChainStep[] plain = Chain.Play(settings, analyses);
        ChainStep[] withItOff = Chain.Play(settings, analyses, [.. LevelFeatures.All, Entry(counting, on: false)]);
        ChainStep[] withItOn = Chain.Play(settings, analyses, [.. LevelFeatures.All, Entry(counting, on: true)]);

        Assert.Equal(plain.Select(step => step.Frame), withItOff.Select(step => step.Frame));
        Assert.Equal(analyses.Length, counting.Buffers);
        Assert.All(withItOn, step => Assert.Equal(0.5 * plain[Array.IndexOf(withItOn, step)].Frame.Strength, step.Frame.Strength, precision: 9));
    }

    private static LevelFeatureEntry Entry(ILevelFeature feature, bool on)
    {
        return new LevelFeatureEntry("test", _ => on, (settings, _) => settings, _ => feature);
    }

    private static void Play(Accompanist accompanist, float[] signal, float[] output)
    {
        // A plain loop: the test measures this thread's allocations, so the loop itself must not allocate.
        for (int start = 0; start + output.Length <= signal.Length; start += output.Length)
        {
            accompanist.Process(signal.AsSpan(start, output.Length), output);
        }
    }

    // Runs after dynamics, halves the strength and counts the buffers it saw.
    private sealed class CountingFeature : ILevelFeature
    {
        public int Buffers { get; private set; }

        public void AfterHarmony(in AnalysisFrame analysis, ref AccompanimentFrame frame)
        {
            Buffers++;
            frame.Strength *= 0.5;
        }
    }
}
