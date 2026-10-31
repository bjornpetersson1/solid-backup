using SolidBackup.Measurement;

namespace SolidBackup.Tests.Measurement;

internal static class ReferenceChecks
{
    /// <summary>Asserts that the segments are ordered, contiguous and cover the clip from 0 to its duration.</summary>
    public static void AssertCovers(ClipReference reference, double durationMs, string clip)
    {
        Assert.True(reference.Segments.Count > 0, $"{clip}: no segments");
        Assert.Equal(0, reference.Segments[0].StartMs);
        for (int i = 0; i < reference.Segments.Count; i++)
        {
            ReferenceSegment segment = reference.Segments[i];
            Assert.True(segment.EndMs > segment.StartMs, $"{clip}: empty segment at {segment.StartMs} ms");
            if (i > 0)
            {
                Assert.Equal(reference.Segments[i - 1].EndMs, segment.StartMs);
            }
        }

        Assert.Equal(durationMs, reference.DurationMs, 0.5);
    }
}
