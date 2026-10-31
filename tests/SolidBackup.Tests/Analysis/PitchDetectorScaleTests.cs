using SolidBackup.Analysis;
using SolidBackup.Measurement;
using SolidBackup.Tests.Measurement;

namespace SolidBackup.Tests.Analysis;

/// <summary>
/// Every note of the generated scales, at both supported sample rates. One class per waveform, so xUnit
/// runs them in parallel; each scale takes seconds in a Debug build.
/// </summary>
public abstract class PitchDetectorScaleTests(string clipName)
{
    private const int Buffer = 64;

    [Theory]
    [InlineData(48_000)]
    [InlineData(44_100)]
    public void EveryNoteFromE2ToC7_IsFoundWithin10Cents(int sampleRate)
    {
        CorpusClip clip = TestCorpus.At(sampleRate).Single(clip => clip.Name == clipName);
        PitchDetector detector = new(sampleRate, CorpusRange.LowestMidi, CorpusRange.HighestMidi, TestSettings.Default.PitchConfidenceThreshold);
        HashSet<int> found = [];

        for (int offset = 0; offset + Buffer <= clip.Samples.Length; offset += Buffer)
        {
            PitchEstimate estimate = detector.Process(clip.Samples.AsSpan(offset, Buffer));

            // Away from the note's edges, where the window still holds the gap or the hold is running.
            if (clip.Reference.SegmentAt(estimate.TimeMs) is { IsVoiced: true } note
                && estimate.TimeMs - note.StartMs > 30 && note.EndMs - estimate.TimeMs > 30)
            {
                Assert.True(estimate.HasNote, $"no note at {estimate.TimeMs:0} ms");
                Assert.InRange(estimate.MidiCents - note.Pitches[0], -10, 10);
                found.Add(estimate.MidiNote);
            }
        }

        Assert.Equal(Enumerable.Range(CorpusRange.LowestMidi, CorpusRange.HighestMidi - CorpusRange.LowestMidi + 1), found.Order());
    }
}

public sealed class PitchDetectorSineScaleTests() : PitchDetectorScaleTests("generated-sine-scale");

public sealed class PitchDetectorSawtoothScaleTests() : PitchDetectorScaleTests("generated-sawtooth-scale");
