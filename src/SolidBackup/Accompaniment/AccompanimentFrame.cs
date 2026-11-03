using SolidBackup.Harmony;
using SolidBackup.Synthesis;

namespace SolidBackup.Accompaniment;

/// <summary>
/// What the synthesizer plays for one buffer. Each aspect has one owner among the level features: whether anything
/// sounds (gate), whether the chord is struck anew (onsets), which tones (chord character) and how strong (dynamics).
/// <see cref="Neutral"/> is what plays with every feature off.
/// </summary>
public record struct AccompanimentFrame(Chord? Chord, bool Sounding, bool Strike, ChordShape Shape, double Strength)
{
    /// <summary>A ready note waits for its beat in the harmony, so the onsets' strike waits for it too.</summary>
    public bool ChangeAwaitsBeat { get; init; }

    public static AccompanimentFrame Neutral(Chord? chord, bool sounding)
    {
        return new AccompanimentFrame(chord, sounding, Strike: false, ChordShape.Whole, Strength: 1);
    }
}
