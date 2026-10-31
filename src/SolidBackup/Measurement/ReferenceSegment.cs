namespace SolidBackup.Measurement;

/// <summary>
/// One stretch of a clip, from <see cref="StartMs"/> up to <see cref="EndMs"/>. A note's pitch is in MIDI
/// cents (A4 = 6900), given as one or more values spaced evenly from start to end and joined linearly:
/// one value for a steady note, two for a glide, and one every 10 ms for a recorded note's measured curve.
/// </summary>
public sealed record ReferenceSegment(double StartMs, double EndMs, SegmentKind Kind, IReadOnlyList<double> Pitches)
{
    public ReferenceSegment(double startMs, double endMs, SegmentKind kind)
        : this(startMs, endMs, kind, [])
    {
    }

    public bool IsVoiced => Kind == SegmentKind.Note;

    public bool IsUnvoiced => Kind is SegmentKind.Silence or SegmentKind.Breath or SegmentKind.Noise;

    /// <summary>A note, in or out of the measured range; only these carry pitch values.</summary>
    public bool HasPitch => Kind is SegmentKind.Note or SegmentKind.OutOfRange;

    /// <summary>The median of the pitch values; NaN for a segment without pitch.</summary>
    public double MedianPitch => Pitches.Count == 0 ? double.NaN : Pitches.Order().ElementAt(Pitches.Count / 2);

    public bool Contains(double timeMs)
    {
        return timeMs >= StartMs && timeMs < EndMs;
    }

    public double? PitchAt(double timeMs)
    {
        if (Pitches.Count <= 1)
        {
            return Pitches.Count == 0 ? null : Pitches[0];
        }

        double position = Math.Clamp((timeMs - StartMs) / (EndMs - StartMs), 0, 1) * (Pitches.Count - 1);
        int index = Math.Min((int)position, Pitches.Count - 2);
        return Pitches[index] + ((Pitches[index + 1] - Pitches[index]) * (position - index));
    }

    public bool Equals(ReferenceSegment? other)
    {
        return other is not null && (StartMs, EndMs, Kind) == (other.StartMs, other.EndMs, other.Kind) && Pitches.SequenceEqual(other.Pitches);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(StartMs, EndMs, Kind, Pitches.Count);
    }
}
