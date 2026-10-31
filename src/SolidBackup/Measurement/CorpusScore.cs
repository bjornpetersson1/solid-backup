namespace SolidBackup.Measurement;

public sealed record ClipScore(string Clip, ScoreCounts Counts);

/// <summary>The scores per clip and in total.</summary>
public sealed record CorpusScore(IReadOnlyList<ClipScore> Clips)
{
    public ScoreCounts Total => Clips.Aggregate(ScoreCounts.Zero, (sum, clip) => sum + clip.Counts);
}
