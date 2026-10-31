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
}
