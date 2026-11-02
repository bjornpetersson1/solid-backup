namespace SolidBackup.Configuration;

/// <summary>Parses settings whose value is the name of an enum member.</summary>
public static class SettingNames
{
    /// <summary>Exact enum names only, so "church" or "3" are rejected.</summary>
    public static bool TryParse<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        return Enum.GetNames<TEnum>().Contains(value) && Enum.TryParse(value, out result);
    }
}
