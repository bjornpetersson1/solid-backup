namespace SolidBackup.Harmony;

/// <summary>Pitch classes 0–11 (C = 0) and their spelling in chord symbols.</summary>
public static class PitchClass
{
    private static readonly string[] Names = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    public static string Name(int pitchClass)
    {
        return Names[pitchClass];
    }

    /// <summary>A MIDI note spelled like the chord roots, with its octave, e.g. 61 → "Db4".</summary>
    public static string NoteName(int midiNumber)
    {
        return $"{Names[midiNumber % 12]}{(midiNumber / 12) - 1}";
    }

    /// <summary>The interval in semitones (0–11) from <paramref name="from"/> up to <paramref name="to"/>.</summary>
    public static int Interval(int from, int to)
    {
        return ((to - from) % 12 + 12) % 12;
    }

    /// <summary>Parses a tonic like "C", "F#" or "Bb" (no octave), with a capital letter like the other harmony settings.</summary>
    public static bool TryParse(string? name, out int pitchClass)
    {
        pitchClass = 0;
        if (name is not { Length: 1 or 2 } || !char.IsAsciiLetterUpper(name[0]) || !Configuration.NoteName.TryParseMidiNumber($"{name}4", out int midi))
        {
            return false;
        }

        pitchClass = midi % 12;
        return true;
    }
}
