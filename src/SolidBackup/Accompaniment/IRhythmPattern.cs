using SolidBackup.Synthesis;

namespace SolidBackup.Accompaniment;

/// <summary>
/// Turns each buffer's accompaniment into tones on the synthesizer: when tones start and stop. The level features
/// decide which tones, how strong and whether anything sounds; the pattern decides when (E5-5 adds Arpeggio and
/// Strum beside <see cref="HeldPattern"/>). Implementations allocate nothing per buffer.
/// </summary>
public interface IRhythmPattern
{
    void Play(in AnalysisFrame analysis, in AccompanimentFrame frame, Synthesizer synthesizer);
}
