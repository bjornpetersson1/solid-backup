namespace SolidBackup.Harmony;

/// <summary>Parses the harmony settings' text values.</summary>
public static class HarmonySettings
{
    public const string AutoKey = "auto";

    public static bool IsValidKey(string? value)
    {
        return value == AutoKey || PitchClass.TryParse(value, out _);
    }
}
