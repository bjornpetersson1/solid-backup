using SolidBackup.Measurement;

namespace SolidBackup.Tests.Measurement;

public sealed class ReferenceFileTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ref.csv");

    public void Dispose()
    {
        File.Delete(_path);
    }

    [Fact]
    public void WrittenReference_ReadsBackTheSame()
    {
        ClipReference reference = new(
            [
                new ReferenceSegment(0, 100.5, SegmentKind.Silence),
                new ReferenceSegment(100.5, 400, SegmentKind.Note, [6912.3]),
                new ReferenceSegment(400, 900, SegmentKind.Note, [4000, 4100.5, 4050, 8400]),
                new ReferenceSegment(900, 950, SegmentKind.Breath),
                new ReferenceSegment(950, 980, SegmentKind.Noise),
                new ReferenceSegment(980, 1000, SegmentKind.Unclear),
                new ReferenceSegment(1000, 1200, SegmentKind.OutOfRange, [9000]),
            ],
            [100.5, 400]);

        ReferenceFile.Write(_path, reference);
        ClipReference read = ReferenceFile.Read(_path);

        Assert.Equal(reference.Segments, read.Segments);
        Assert.Equal(reference.OnsetsMs, read.OnsetsMs);
    }

    [Theory]
    [InlineData("segment,0,100,note", "tonhöjd saknas")]
    [InlineData("segment,0,100,silence,6900", "tonhöjd för ett segment utan ton")]
    [InlineData("segment,0,100,hum", "okänd typ 'hum'")]
    [InlineData("onset,abc", "'abc' är inget tal")]
    [InlineData("pitch,0", "okänd rad")]
    [InlineData("segment,10,100,silence", "segmentet 10–100 ms följer inte direkt på det förra (som slutar vid 0 ms)")]
    [InlineData("segment,0,0,silence", "segmentet 0–0 ms följer inte direkt på det förra (som slutar vid 0 ms)")]
    public void InvalidLine_GivesSwedishErrorWithLineNumber(string line, string problem)
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => ReferenceFile.Parse(["# comment", "", line], "test.ref.csv"));

        Assert.Equal($"Referensfilen test.ref.csv, rad 3: {problem}.", error.Message);
    }

    [Fact]
    public void Glide_PitchMovesLinearlyInCents()
    {
        ReferenceSegment glide = new(1000, 2000, SegmentKind.Note, [4000, 8000]);

        Assert.Equal(4000, glide.PitchAt(1000));
        Assert.Equal(6000, glide.PitchAt(1500));
        Assert.Equal(8000, glide.PitchAt(2000));
    }

    [Fact]
    public void PitchCurve_IsJoinedLinearlyBetweenEvenlySpacedValues()
    {
        ReferenceSegment note = new(0, 20, SegmentKind.Note, [6900, 6950, 6930]);

        Assert.Equal(6900, note.PitchAt(0));
        Assert.Equal(6925, note.PitchAt(5));
        Assert.Equal(6950, note.PitchAt(10));
        Assert.Equal(6940, note.PitchAt(15));
        Assert.Equal(6930, note.PitchAt(20));
        Assert.Equal(6930, note.MedianPitch);
    }
}
