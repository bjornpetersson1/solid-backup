namespace SolidBackup.Configuration;

public static class NoteName
{
    // Semitones above C for the letters A to G.
    private static readonly int[] SemitonesAboveC = [9, 11, 0, 2, 4, 5, 7];

    public static bool TryParseMidiNumber(string? name, out int midiNumber)
    {
        midiNumber = 0;
        if (name is null || name.Length < 2)
        {
            return false;
        }

        char letter = char.ToUpperInvariant(name[0]);
        if (letter is < 'A' or > 'G')
        {
            return false;
        }

        int semitone = SemitonesAboveC[letter - 'A'];
        int octaveStart = 1;
        if (name[1] == '#')
        {
            semitone++;
            octaveStart++;
        }
        else if (name[1] == 'b')
        {
            semitone--;
            octaveStart++;
        }

        if (!TryParseOctave(name.AsSpan(octaveStart), out int octave))
        {
            return false;
        }

        midiNumber = ((octave + 1) * 12) + semitone;
        return midiNumber is >= 0 and <= 127;
    }

    // Octaves -1 to 9 cover the MIDI range; anything else, including signs and padding like "+2" or "02", is rejected.
    private static bool TryParseOctave(ReadOnlySpan<char> text, out int octave)
    {
        octave = text switch
        {
            "-1" => -1,
            [char digit] when char.IsAsciiDigit(digit) => digit - '0',
            _ => int.MinValue,
        };
        return octave != int.MinValue;
    }
}
