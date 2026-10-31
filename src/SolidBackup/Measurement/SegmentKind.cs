namespace SolidBackup.Measurement;

/// <summary>What a stretch of a corpus clip holds.</summary>
public enum SegmentKind
{
    /// <summary>A pitched note within the measured range; counted as voiced.</summary>
    Note,

    /// <summary>Silence; counted as unvoiced.</summary>
    Silence,

    /// <summary>Breath noise; counted as unvoiced.</summary>
    Breath,

    /// <summary>Other unpitched noise; counted as unvoiced.</summary>
    Noise,

    /// <summary>A note outside the measured range; left out of every measure.</summary>
    OutOfRange,

    /// <summary>
    /// A loud stretch around a recorded note that is not periodic enough to label (an attack or a decay);
    /// left out of every measure, since a detector may fairly report a note there or not.
    /// </summary>
    Unclear,
}
