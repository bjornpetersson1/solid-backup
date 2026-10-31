namespace SolidBackup.Harmony;

/// <summary>A tonic pitch class (0 = C) and a mode.</summary>
public readonly record struct Key(int Tonic, Mode Mode)
{
    private static readonly int[] IonianSteps = [0, 2, 4, 5, 7, 9, 11];

    /// <summary>Ionian, Lydian and Mixolydian have a major third; the styles without modes treat them as major.</summary>
    public bool IsMajor => Mode is Mode.Ionian or Mode.Lydian or Mode.Mixolydian;

    /// <summary>The same tonic in Ionian or Aeolian, for the styles that only know major and minor.</summary>
    public Key MajorOrMinor => this with { Mode = IsMajor ? Mode.Ionian : Mode.Aeolian };

    /// <summary>The pitch class of a scale degree, 1 to 7; higher degrees wrap into the next octave.</summary>
    public int PitchClassOf(int degree)
    {
        int index = (int)Mode + degree - 1;
        int semitones = IonianSteps[index % 7] - IonianSteps[(int)Mode] + (12 * (index / 7));
        return (Tonic + semitones) % 12;
    }

    // The generated record ToString would print MajorOrMinor, itself a Key, and recurse without end.
    public override string ToString()
    {
        return $"{PitchClass.Name(Tonic)} {Mode}";
    }

    /// <summary>The triad built from the scale on a degree, 1 to 7.</summary>
    public Chord TriadOn(int degree)
    {
        int root = PitchClassOf(degree);
        int third = PitchClass.Interval(root, PitchClassOf(degree + 2));
        int fifth = PitchClass.Interval(root, PitchClassOf(degree + 4));
        ChordQuality quality = (third, fifth) switch
        {
            (4, 7) => ChordQuality.Major,
            (3, 7) => ChordQuality.Minor,
            _ => ChordQuality.Diminished,
        };
        return new Chord(root, quality);
    }
}
