using SolidBackup.Configuration;
using SolidBackup.Rhythm;

namespace SolidBackup.Harmony;

/// <summary>
/// When the chord may change; see "When the chord may change" in <c>docs/harmony.md</c>. With
/// <see cref="ChangeBeats"/>, a change while singing waits for the next allowed beat; null is <c>Off</c>.
/// </summary>
public readonly record struct HarmonyTiming(double MinNoteForChangeMs, double MinChordDurationMs, double PhraseResetMs, ChangeBeats? ChangeBeats = null)
{
    /// <summary>The hold times scaled by the sensitivity, and the allowed beats on <paramref name="grid"/>, the settings' grid.</summary>
    /// <exception cref="ArgumentException">The settings are not valid; validate them first.</exception>
    public static HarmonyTiming FromSettings(AppSettings settings, TempoGrid grid)
    {
        if (!ChordChangeSettings.TryParsePerBar(settings.ChordChangesPerBar, out ChordChangesPerBar perBar)
            || !SettingNames.TryParse(settings.ChordChangeSensitivity, out ChordChangeSensitivity sensitivity))
        {
            throw new ArgumentException("The chord change settings are not valid; validate them first.", nameof(settings));
        }

        double factor = ChordChangeSettings.HoldFactor(sensitivity);
        return new HarmonyTiming(
            settings.MinNoteForChangeMs * factor,
            settings.MinChordDurationMs * factor,
            settings.PhraseResetMs,
            Harmony.ChangeBeats.Create(grid, perBar));
    }
}
