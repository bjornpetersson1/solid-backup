using SolidBackup.Synthesis;

namespace SolidBackup.Accompaniment;

/// <summary>
/// Chord character: from the smoothed level, a soft voice gives a
/// sparse chord (below <c>CharacterSparseBelowDbfs</c>), a loud one a full chord (above <c>CharacterFullAboveDbfs</c>),
/// and the whole chord between them. The level must pass a boundary by <see cref="HysteresisDb"/> before the
/// character changes, so a level hovering at it does not flip.
/// </summary>
public sealed class ChordCharacterFeature(double sparseBelowDbfs, double fullAboveDbfs) : ILevelFeature
{
    public const double HysteresisDb = 3;

    private ChordShape? _shape;

    public void AfterHarmony(in AnalysisFrame analysis, ref AccompanimentFrame frame)
    {
        _shape = Next(_shape, analysis.SmoothedLevelDbfs);
        frame.Shape = _shape.Value;
    }

    private ChordShape Next(ChordShape? current, double level)
    {
        ChordShape plain = level < sparseBelowDbfs ? ChordShape.Sparse : level > fullAboveDbfs ? ChordShape.Full : ChordShape.Whole;
        if (current is not { } shape || plain == shape)
        {
            return plain;
        }

        // Leaving the current character needs the level past its boundary by the hysteresis.
        bool leaves = shape switch
        {
            ChordShape.Sparse => level >= sparseBelowDbfs + HysteresisDb,
            ChordShape.Full => level <= fullAboveDbfs - HysteresisDb,
            _ => level < sparseBelowDbfs - HysteresisDb || level > fullAboveDbfs + HysteresisDb,
        };
        return leaves ? plain : shape;
    }
}
