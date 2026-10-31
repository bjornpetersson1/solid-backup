using SolidBackup.Harmony;

namespace SolidBackup.Cli;

/// <summary>The Swedish names of the harmony styles and modes shown in the console.</summary>
public static class HarmonyNames
{
    public static string Style(HarmonyStyle style)
    {
        return style switch
        {
            HarmonyStyle.Church => "Kyrko",
            HarmonyStyle.Campfire => "Lägereld",
            HarmonyStyle.Drone => "Bordun",
            HarmonyStyle.Pop => "Pop",
            HarmonyStyle.Modal => "Modal",
            HarmonyStyle.Blues => "Blues",
            HarmonyStyle.Jazz => "Jazz",
            HarmonyStyle.Free => "Fri",
            _ => throw new ArgumentOutOfRangeException(nameof(style)),
        };
    }

    public static string Mode(Mode mode)
    {
        return mode switch
        {
            Harmony.Mode.Ionian => "jonisk",
            Harmony.Mode.Dorian => "dorisk",
            Harmony.Mode.Phrygian => "frygisk",
            Harmony.Mode.Lydian => "lydisk",
            Harmony.Mode.Mixolydian => "mixolydisk",
            Harmony.Mode.Aeolian => "eolisk",
            Harmony.Mode.Locrian => "lokrisk",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }
}
