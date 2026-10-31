using SolidBackup.Measurement;

namespace SolidBackup.Tests.Measurement;

/// <summary>The generated corpus at 48 kHz, built once for all tests.</summary>
internal static class TestCorpus
{
    private static readonly Lazy<IReadOnlyList<CorpusClip>> LazyGenerated = new(() => GeneratedCorpus.Create(48_000));

    public static IReadOnlyList<CorpusClip> Generated => LazyGenerated.Value;

    public static CorpusClip Clip(string name)
    {
        return Generated.Single(clip => clip.Name == name);
    }
}
