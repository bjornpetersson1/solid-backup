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

    /// <summary>How often the chord may change, e.g. "2 per takt, känslighet medel".</summary>
    public static string ChordChanges(AppSettings settings)
    {
        string perBar = ChordChangeSettings.TryParsePerBar(settings.ChordChangesPerBar, out ChordChangesPerBar parsed)
            ? parsed is ChordChangesPerBar.One or ChordChangesPerBar.Two ? $"{ChangesPerBar(parsed)} per takt" : ChangesPerBar(parsed).ToLowerInvariant()
            : settings.ChordChangesPerBar;
        string sensitivity = SettingNames.TryParse(settings.ChordChangeSensitivity, out ChordChangeSensitivity value)
            ? Sensitivity(value).ToLowerInvariant()
            : settings.ChordChangeSensitivity;
        return $"{perBar}, känslighet {sensitivity}";
    }

    /// <summary>The menu's name for a value of <c>ChordChangesPerBar</c>: Av, 1, 2 or Varje slag.</summary>
    public static string ChangesPerBar(ChordChangesPerBar perBar)
    {
        return perBar switch
        {
            ChordChangesPerBar.Off => "Av",
            ChordChangesPerBar.One => "1",
            ChordChangesPerBar.Two => "2",
            ChordChangesPerBar.Beat => "Varje slag",
            _ => perBar.ToString(),
        };
    }

    public static string Sensitivity(ChordChangeSensitivity sensitivity)
    {
        return sensitivity switch
        {
            ChordChangeSensitivity.Low => "Låg",
            ChordChangeSensitivity.Medium => "Medel",
            ChordChangeSensitivity.High => "Hög",
            _ => sensitivity.ToString(),
        };
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
