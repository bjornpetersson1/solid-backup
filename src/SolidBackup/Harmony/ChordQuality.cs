namespace SolidBackup.Harmony;

public enum ChordQuality
{
    Major,
    Minor,
    Diminished,
    /// <summary>Root and fifth only.</summary>
    Power,

    /// <summary>A major triad with the ninth (Pop's colour).</summary>
    Add9,

    /// <summary>A minor triad with the ninth (Pop's colour).</summary>
    MinorAdd9,

    /// <summary>Root, fourth and fifth (Pop's colour on a major chord).</summary>
    Sus4,

    /// <summary>A major triad with the minor seventh.</summary>
    Dominant7,

    /// <summary>A major triad with the major seventh.</summary>
    Major7,

    /// <summary>A minor triad with the minor seventh.</summary>
    Minor7,

    /// <summary>A diminished triad with the minor seventh (m7b5).</summary>
    HalfDiminished7,

    /// <summary>A diminished triad with the diminished seventh.</summary>
    Diminished7,
}
