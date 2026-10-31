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

    /// <summary>A key for display: "C-dur" and "A-moll" for Ionian and Aeolian, else the tonic and the mode, e.g. "D dorisk".</summary>
    public static string Key(Key key)
    {
        string tonic = PitchClass.Name(key.Tonic);
        return key.Mode switch
        {
            Harmony.Mode.Ionian => $"{tonic}-dur",
            Harmony.Mode.Aeolian => $"{tonic}-moll",
            _ => $"{tonic} {Mode(key.Mode)}",
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
