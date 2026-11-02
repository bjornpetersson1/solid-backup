namespace SolidBackup.Synthesis;

/// <summary>Which of a voiced chord's tones sound (chord character, E5-2).</summary>
public enum ChordShape
{
    /// <summary>The root and the perfect fifth, or the root and the third when the chord has no perfect fifth.</summary>
    Sparse,

    /// <summary>Every tone of the chord.</summary>
    Whole,

    /// <summary>Every tone, plus the root an octave below the voiced root.</summary>
    Full,
}
