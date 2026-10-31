using System.Diagnostics.CodeAnalysis;

namespace SolidBackup.Harmony;

public static class ChordRules
{
    /// <summary>The rules for a style, or false for a style that is not built yet.</summary>
    public static bool TryCreate(HarmonyStyle style, [NotNullWhen(true)] out IChordRules? rules)
    {
        rules = style switch
        {
            HarmonyStyle.Church => new ChurchRules(),
            HarmonyStyle.Campfire => new CampfireRules(),
            HarmonyStyle.Drone => new DroneRules(),
            _ => null,
        };
        return rules is not null;
    }
}
