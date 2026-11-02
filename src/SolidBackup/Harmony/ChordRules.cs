using SolidBackup.Rhythm;

namespace SolidBackup.Harmony;

public static class ChordRules
{
    /// <summary>The rules for a style; Jazz keeps time on <paramref name="grid"/>.</summary>
    public static IChordRules Create(HarmonyStyle style, TempoGrid grid)
    {
        return style switch
        {
            HarmonyStyle.Church => new ChurchRules(),
            HarmonyStyle.Campfire => new CampfireRules(),
            HarmonyStyle.Drone => new DroneRules(),
            HarmonyStyle.Pop => new PopRules(),
            HarmonyStyle.Modal => new ModalRules(),
            HarmonyStyle.Blues => new BluesRules(),
            HarmonyStyle.Jazz => new JazzRules(grid),
            HarmonyStyle.Free => new FreeRules(),
            _ => throw new ArgumentOutOfRangeException(nameof(style)),
        };
    }
}
