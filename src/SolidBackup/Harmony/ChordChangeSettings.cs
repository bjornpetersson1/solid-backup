namespace SolidBackup.Harmony;

/// <summary>The most chord changes a bar may have while singing, as the <c>ChordChangesPerBar</c> setting names them.</summary>
public enum ChordChangesPerBar
{
    /// <summary>No grid: a change comes once its hold times have passed.</summary>
    Off,

    /// <summary>Beat 1 of each bar.</summary>
    One,

    /// <summary>Beats 1 and 3 of each bar, in 4/4 and 3/4 alike.</summary>
    Two,

    /// <summary>Every beat.</summary>
    Beat,
}

/// <summary>How easily a new note changes the chord: a factor on both hold times.</summary>
public enum ChordChangeSensitivity
{
    Low,
    Medium,
    High,
}

/// <summary>Parses the chord change settings; see "When the chord may change" in <c>docs/harmony.md</c>.</summary>
public static class ChordChangeSettings
{
    private static readonly (string Name, ChordChangesPerBar Value)[] PerBarNames =
        [("Off", ChordChangesPerBar.Off), ("1", ChordChangesPerBar.One), ("2", ChordChangesPerBar.Two), ("Beat", ChordChangesPerBar.Beat)];

    /// <summary>The setting's values, in the order the menu shows them.</summary>
    public static IReadOnlyList<string> AllPerBar { get; } = Array.AsReadOnly(PerBarNames.Select(entry => entry.Name).ToArray());

    /// <summary>Exact setting values only, so "off" or "3" are rejected.</summary>
    public static bool TryParsePerBar(string? value, out ChordChangesPerBar perBar)
    {
        int index = Array.FindIndex(PerBarNames, entry => entry.Name == value);
        perBar = index >= 0 ? PerBarNames[index].Value : default;
        return index >= 0;
    }

    /// <summary>The factor on <c>MinNoteForChangeMs</c> and <c>MinChordDurationMs</c>: Low 2, Medium 1, High 0.5.</summary>
    public static double HoldFactor(ChordChangeSensitivity sensitivity)
    {
        return sensitivity switch
        {
            ChordChangeSensitivity.Low => 2,
            ChordChangeSensitivity.Medium => 1,
            ChordChangeSensitivity.High => 0.5,
            _ => throw new ArgumentOutOfRangeException(nameof(sensitivity)),
        };
    }
}
