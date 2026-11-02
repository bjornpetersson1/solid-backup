using SolidBackup.Analysis;

namespace SolidBackup.Accompaniment;

/// <summary>
/// The gate as a level feature: a note reaches the harmony only while E3-3's noise gate is open, and when it closes
/// every tone is released. The chord comes back only when the gate is open and a note sounds, so breath or noise
/// that opens the gate alone does not bring it back.
/// </summary>
public sealed class GateFeature : ILevelFeature
{
    private bool _closed = true;

    public void BeforeHarmony(in AnalysisFrame analysis, ref HarmonyInput input)
    {
        if (!analysis.GateOpen && input.Estimate.HasNote)
        {
            input = input with { Estimate = PitchEstimate.NoNote(input.Estimate.TimeMs, input.Estimate.Confidence) };
        }
    }

    public void AfterHarmony(in AnalysisFrame analysis, ref AccompanimentFrame frame)
    {
        _closed = !analysis.GateOpen || (_closed && !analysis.Estimate.HasNote);
        frame.Sounding &= !_closed;
    }
}
