using SolidBackup.Measurement;

namespace SolidBackup.Tests.Measurement;

public sealed class ReferenceLabelerTests
{
    private static readonly int[] Range = Enumerable.Range(CorpusRange.LowestMidi, CorpusRange.HighestMidi - CorpusRange.LowestMidi + 1).ToArray();

    // Two notes with a gap, short enough to label quickly.
    private static readonly CorpusClip TwoNotes = new(
        "two-notes",
        48_000,
        [.. TestCorpus.Clip("generated-sine-scale").Samples.AsSpan(0, 48_000 * 9 / 10)],
        new ClipReference([], []));

    [Theory]
    [InlineData("generated-sine-scale")]
    [InlineData("generated-sawtooth-noise-scale")]
    public void GeneratedScale_IsLabelledLikeItsGeneratedReference(string name)
    {
        CorpusClip clip = TestCorpus.Clip(name);

        ClipReference label = ReferenceLabeler.Label(clip.Samples, clip.SampleRate, new ScriptedClip(name, RecordingKind.Voice, Range));

        ReferenceChecks.AssertCovers(label, clip.Reference.DurationMs, name);
        ReferenceSegment[] notes = label.Segments.Where(segment => segment.IsVoiced).ToArray();
        Assert.Equal(Range.Length, notes.Length);
        for (int i = 0; i < Range.Length; i++)
        {
            Assert.Equal(Range[i] * 100.0, notes[i].MedianPitch, tolerance: 5);
            // Away from the note's ends, where the analysis window reaches into the fade or the gap, the curve is exact.
            Assert.All(notes[i].Pitches.Skip(3).SkipLast(3), pitch => Assert.Equal(Range[i] * 100.0, pitch, tolerance: 5));
        }

        Assert.Equal(clip.Reference.OnsetsMs.Count, label.OnsetsMs.Count);
        Assert.All(label.OnsetsMs.Zip(clip.Reference.OnsetsMs), pair => Assert.Equal(pair.Second, pair.First, tolerance: 5));
    }

    [Fact]
    public void NoteListWithTheWrongNumberOfNotes_IsRejected()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => ReferenceLabeler.Label(TwoNotes.Samples, TwoNotes.SampleRate, new ScriptedClip("scale", RecordingKind.Voice, [40])));

        Assert.Equal("Klippet scale har 2 toner i inspelningen men 1 i listan.", error.Message);
    }

    [Fact]
    public void NotesListedOutsideTheRange_AreLabelledOutOfRange()
    {
        // The clip holds E2 and F2; listing the first as D#2, below the range, makes it out of range.
        ClipReference label = ReferenceLabeler.Label(TwoNotes.Samples, TwoNotes.SampleRate, new ScriptedClip("scale", RecordingKind.Voice, [39, 41]));

        Assert.Equal([SegmentKind.OutOfRange, SegmentKind.Note], label.SungSegments().Select(segment => segment.Kind));
        Assert.Single(label.OnsetsMs);
    }

    [Fact]
    public void SuggestedNotes_AreTheGeneratedScale()
    {
        CorpusClip clip = TestCorpus.Clip("generated-sawtooth-scale");

        Assert.Equal(Range, ReferenceLabeler.SuggestNotes(clip.Samples, clip.SampleRate));
    }

    [Theory]
    [InlineData(RecordingKind.Silence, SegmentKind.Silence)]
    [InlineData(RecordingKind.Breath, SegmentKind.Breath)]
    public void UnpitchedClip_IsOneSegment(RecordingKind kind, SegmentKind expected)
    {
        ClipReference label = ReferenceLabeler.Label(new float[48_000], 48_000, new ScriptedClip("clip", kind, []));

        Assert.Equal([new ReferenceSegment(0, 1000, expected)], label.Segments);
        Assert.Empty(label.OnsetsMs);
    }
}
