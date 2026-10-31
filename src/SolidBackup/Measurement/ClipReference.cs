namespace SolidBackup.Measurement;

/// <summary>The reference for one clip: contiguous segments covering it, its onsets, and for a melody its keys.</summary>
public sealed record ClipReference(IReadOnlyList<ReferenceSegment> Segments, IReadOnlyList<double> OnsetsMs)
{
    /// <summary>The keys of a generated melody, in time order; empty for every other clip.</summary>
    public IReadOnlyList<ReferenceKey> Keys { get; init; } = [];

    public double DurationMs => Segments.Count == 0 ? 0 : Segments[^1].EndMs;

    /// <summary>The segment holding <paramref name="timeMs"/>, found by binary search over the ordered segments.</summary>
    public ReferenceSegment? SegmentAt(double timeMs)
    {
        int low = 0;
        int high = Segments.Count - 1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            ReferenceSegment segment = Segments[middle];
            if (segment.Contains(timeMs))
            {
                return segment;
            }

            (low, high) = timeMs < segment.StartMs ? (low, middle - 1) : (middle + 1, high);
        }

        return null;
    }

    /// <summary>The notes, in or out of range, in order, as the note list gives them.</summary>
    public IEnumerable<ReferenceSegment> SungSegments()
    {
        return Segments.Where(segment => segment.HasPitch);
    }
}
