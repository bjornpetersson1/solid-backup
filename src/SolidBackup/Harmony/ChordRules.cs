using System.Diagnostics.CodeAnalysis;

namespace SolidBackup.Harmony;

public static class ChordRules
{
    /// <summary>Whether the style's rules exist yet, without building them; agrees with <see cref="TryCreate"/>.</summary>
    public static bool IsBuilt(HarmonyStyle style)
    {
        return style is HarmonyStyle.Church or HarmonyStyle.Campfire or HarmonyStyle.Drone or HarmonyStyle.Pop or HarmonyStyle.Modal;
    }

    /// <summary>The rules for a style, or false for a style that is not built yet.</summary>
    public static bool TryCreate(HarmonyStyle style, [NotNullWhen(true)] out IChordRules? rules)
    {
        rules = style switch
        {
            HarmonyStyle.Church => new ChurchRules(),
            HarmonyStyle.Campfire => new CampfireRules(),
            HarmonyStyle.Drone => new DroneRules(),
            HarmonyStyle.Pop => new PopRules(),
            HarmonyStyle.Modal => new ModalRules(),
            _ => null,
        };
        return rules is not null;
    }
}
