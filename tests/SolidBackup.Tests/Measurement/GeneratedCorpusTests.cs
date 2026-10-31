using SolidBackup.Measurement;

namespace SolidBackup.Tests.Measurement;

public sealed class GeneratedCorpusTests
{
    [Fact]
    public void GeneratingTwice_GivesIdenticalSamples()
    {
        IReadOnlyList<CorpusClip> first = TestCorpus.Generated;
        IReadOnlyList<CorpusClip> second = GeneratedCorpus.Create(48_000);

        Assert.Equal(first.Select(clip => clip.Name), second.Select(clip => clip.Name));
        for (int i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Samples, second[i].Samples);
            Assert.Equal(first[i].Reference.Segments, second[i].Reference.Segments);
        }
    }

    [Theory]
    [InlineData(48_000)]
    [InlineData(44_100)]
    public void References_CoverEveryClip(int sampleRate)
    {
        foreach (CorpusClip clip in sampleRate == 48_000 ? TestCorpus.Generated : GeneratedCorpus.Create(sampleRate))
        {
            Assert.Equal(sampleRate, clip.SampleRate);
            ReferenceChecks.AssertCovers(clip.Reference, clip.Samples.Length * 1000.0 / sampleRate, clip.Name);
            Assert.All(clip.Samples, sample => Assert.InRange(sample, -1f, 1f));
        }
    }

    [Theory]
    [InlineData("generated-sine-scale")]
    [InlineData("generated-sawtooth-scale")]
    [InlineData("generated-sine-noise-scale")]
    [InlineData("generated-sawtooth-noise-scale")]
    public void Scales_HoldEverySemitoneOfTheRange_WithAnOnsetEach(string name)
    {
        CorpusClip clip = Clip(name);

        double[] notes = clip.Reference.Segments.Where(segment => segment.IsVoiced).Select(segment => segment.Pitches.Single()).ToArray();
        Assert.Equal(Enumerable.Range(CorpusRange.LowestMidi, CorpusRange.HighestMidi - CorpusRange.LowestMidi + 1).Select(midi => midi * 100.0), notes);
        Assert.Equal(CorpusRange.HighestMidi - CorpusRange.LowestMidi + 1, clip.Reference.OnsetsMs.Count);
        Assert.Equal(GeneratedCorpus.NoteMs, clip.Reference.Segments.First(segment => segment.IsVoiced).EndMs - GeneratedCorpus.GapMs, 0.01);
    }

    [Fact]
    public void NoisyScales_HaveNoiseInsteadOfSilence()
    {
        CorpusClip clip = Clip("generated-sine-noise-scale");

        Assert.DoesNotContain(clip.Reference.Segments, segment => segment.Kind == SegmentKind.Silence);
        Assert.Contains(clip.Reference.Segments, segment => segment.Kind == SegmentKind.Noise);
        Assert.NotEqual(0f, clip.Samples[10]);
    }

    [Fact]
    public void Glides_SweepTheWholeRangeUpAndDown()
    {
        ReferenceSegment[] glides = Clip("generated-glides").Reference.Segments.Where(segment => segment.IsVoiced).ToArray();

        Assert.Equal(2, glides.Length);
        Assert.Equal([4000.0, 9600.0], glides[0].Pitches);
        Assert.Equal([9600.0, 4000.0], glides[1].Pitches);
        Assert.Equal(GeneratedCorpus.GlideMs, glides[0].EndMs - glides[0].StartMs, 0.01);
    }

    [Fact]
    public void UnvoicedClip_IsSilenceAndNoiseOnly()
    {
        CorpusClip clip = Clip("generated-unvoiced");

        Assert.All(clip.Reference.Segments, segment => Assert.True(segment.IsUnvoiced));
        Assert.Empty(clip.Reference.OnsetsMs);
        Assert.Equal(0f, clip.Samples[100]);
    }

    private static CorpusClip Clip(string name)
    {
        return TestCorpus.Clip(name);
    }
}
