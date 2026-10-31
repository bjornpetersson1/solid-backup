using SolidBackup.Configuration;

namespace SolidBackup.Harmony;

/// <summary>When the chord may change; see "When the chord may change" in <c>docs/harmony.md</c>.</summary>
public readonly record struct HarmonyTiming(double MinNoteForChangeMs, double MinChordDurationMs, double PhraseResetMs)
{
    public static HarmonyTiming FromSettings(AppSettings settings)
    {
        return new HarmonyTiming(settings.MinNoteForChangeMs, settings.MinChordDurationMs, settings.PhraseResetMs);
    }
}
