using SolidBackup.Accompaniment;
using SolidBackup.Configuration;
using SolidBackup.Harmony;

namespace SolidBackup.Cli;

/// <summary>The live choices in the settings as the console shows them, in Swedish, for the menu and the dashboard.</summary>
public static class SettingTexts
{
    /// <summary>The fixed key, e.g. "D dorisk", or <c>auto</c>.</summary>
    public static string Key(AppSettings settings)
    {
        return settings.Key != HarmonySettings.AutoKey && PitchClass.TryParse(settings.Key, out int tonic) && SettingNames.TryParse(settings.Mode, out Mode mode)
            ? HarmonyNames.Key(new Key(tonic, mode))
            : HarmonySettings.AutoKey;
    }

    public static string Style(AppSettings settings)
    {
        return SettingNames.TryParse(settings.HarmonyStyle, out HarmonyStyle style) ? HarmonyNames.Style(style) : settings.HarmonyStyle;
    }

    /// <summary>The pattern, tempo and time signature, e.g. "Arpeggio, 90 BPM, 4/4".</summary>
    public static string Rhythm(AppSettings settings)
    {
        return $"{settings.RhythmPattern}, {settings.Bpm} BPM, {settings.TimeSignature}";
    }

    /// <summary>The level features that are on, by their Swedish names, or "inga".</summary>
    public static string Features(AppSettings settings)
    {
        string[] on = [.. LevelFeatures.All.Where(entry => entry.IsOn(settings)).Select(entry => entry.DisplayName)];
        return on.Length == 0 ? "inga" : string.Join(", ", on);
    }

    public static string OnOff(bool on)
    {
        return on ? "på" : "av";
    }
}
