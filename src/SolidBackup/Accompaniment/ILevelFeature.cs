namespace SolidBackup.Accompaniment;

/// <summary>
/// A level feature (gate, dynamics, chord character, onsets, …): a separate component that reads each buffer's
/// analysis and adjusts what reaches the harmony and what the synthesizer plays. Both steps default to doing
/// nothing. Implementations allocate nothing per buffer.
/// </summary>
public interface ILevelFeature
{
    void BeforeHarmony(in AnalysisFrame analysis, ref HarmonyInput input)
    {
    }

    void AfterHarmony(in AnalysisFrame analysis, ref AccompanimentFrame frame)
    {
    }
}
