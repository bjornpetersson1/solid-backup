namespace SolidBackup.Rhythm;

/// <summary>The rhythm patterns, as named in the <c>RhythmPattern</c> setting.</summary>
public enum RhythmPatternName
{
    Held,
    Arpeggio,
    Strum,
}

/// <summary>Parses the rhythm settings' values; see <c>docs/rhythm.md</c>.</summary>
public static class RhythmSettings
{
    public const int MinBpm = 40;
    public const int MaxBpm = 240;

    private static readonly (string Name, int BeatsPerBar)[] TimeSignatures = [("4/4", 4), ("3/4", 3)];

    public static IEnumerable<string> AllTimeSignatures => TimeSignatures.Select(signature => signature.Name);

    /// <summary>The beats per bar of a supported time signature, e.g. 4 for "4/4"; false for anything else.</summary>
    public static bool TryParseTimeSignature(string? value, out int beatsPerBar)
    {
        beatsPerBar = Array.Find(TimeSignatures, signature => signature.Name == value).BeatsPerBar;
        return beatsPerBar > 0;
    }
}
