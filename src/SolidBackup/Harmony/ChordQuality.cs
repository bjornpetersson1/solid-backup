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

    // The three-tone shapes of Free (E7-4) that the qualities above do not cover; Free's order of preference is FreeRules.Shapes.

    /// <summary>Root, major third, minor seventh: 7(no5).</summary>
    Dominant7No5,

    /// <summary>Root, minor third, minor seventh: m7(no5).</summary>
    Minor7No5,

    /// <summary>Root, major third, major seventh: maj7(no5).</summary>
    Major7No5,

    /// <summary>Root, ninth, major third: add9(no5).</summary>
    Add9No5,

    /// <summary>Root, ninth, minor third: madd9(no5).</summary>
    MinorAdd9No5,

    /// <summary>Root, major third, augmented fifth.</summary>
    Augmented,

    /// <summary>Root, fifth, minor seventh: 7(no3).</summary>
    Dominant7No3,

    /// <summary>Root, fifth, major seventh: maj7(no3).</summary>
    Major7No3,

    /// <summary>Root, major third, augmented fourth: (#11,no5).</summary>
    Sharp11No5,

    /// <summary>Root, minor and major third: (#9,no5).</summary>
    Sharp9No5,

    /// <summary>Root, minor third, major seventh: m(maj7,no5).</summary>
    MinorMajor7No5,

    /// <summary>Root, minor second, minor third: m(b9,no5).</summary>
    MinorFlat9No5,

    /// <summary>Root, minor second, fifth: 5(b9).</summary>
    PowerFlat9,

    /// <summary>Root, augmented fourth, fifth: 5(#11).</summary>
    PowerSharp11,

    /// <summary>Root, minor and major second: (b9,9).</summary>
    Cluster,
}
