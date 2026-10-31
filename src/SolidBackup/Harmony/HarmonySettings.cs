namespace SolidBackup.Harmony;

/// <summary>Parses the harmony settings' text values.</summary>
public static class HarmonySettings
{
    public const string AutoKey = "auto";

    /// <summary>Exact enum names only, so "church" or "3" are rejected.</summary>
    public static bool TryParseName<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        return Enum.GetNames<TEnum>().Contains(value) && Enum.TryParse(value, out result);
    }

    public static bool IsValidKey(string? value)
    {
        return value == AutoKey || PitchClass.TryParse(value, out _);
    }
}
