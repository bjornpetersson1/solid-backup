using System.Collections.Concurrent;

using SolidBackup.Measurement;

namespace SolidBackup.Tests.Measurement;

/// <summary>The generated corpus, built once per sample rate for all tests.</summary>
internal static class TestCorpus
{
    private static readonly ConcurrentDictionary<int, Lazy<IReadOnlyList<CorpusClip>>> BySampleRate = new();

    public static IReadOnlyList<CorpusClip> Generated => At(48_000);

    public static IReadOnlyList<CorpusClip> At(int sampleRate)
    {
        return BySampleRate.GetOrAdd(sampleRate, rate => new(() => GeneratedCorpus.Create(rate))).Value;
    }

    public static CorpusClip Clip(string name)
    {
        return Generated.Single(clip => clip.Name == name);
    }
}
