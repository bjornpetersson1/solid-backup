namespace SolidBackup.Harmony;

/// <summary>
/// Modal: the mode's tonic triad as a pedal, and the mode's character chord when its character note sounds;
/// a note of the tonic triad gives the tonic back, any other note keeps the chord. A chord that is neither of
/// the key's two (after a modulation with <c>auto</c>) is not kept: the tonic takes its place.
/// </summary>
public sealed class ModalRules : IChordRules
{
    // Per mode: the character note and the root of the major character chord, in semitones above the tonic.
    private static readonly (int Note, int ChordRoot)[] Character =
    [
        (5, 5), // Ionian: 4 → IV
        (9, 5), // Dorian: 6 → IV
        (1, 1), // Phrygian: b2 → bII
        (6, 2), // Lydian: #4 → II
        (10, 10), // Mixolydian: b7 → bVII
        (8, 8), // Aeolian: b6 → bVI
        (6, 6), // Locrian: b5 → bV
    ];

    private static readonly int ModeCount = Enum.GetValues<Mode>().Length;

    // Per key (tonic × mode): its tonic triad, character note and character chord.
    private readonly (Chord Tonic, int CharacterNote, Chord CharacterChord)[] _keys = new (Chord, int, Chord)[12 * ModeCount];

    public ModalRules()
    {
        for (int tonic = 0; tonic < 12; tonic++)
        {
            foreach (Mode mode in Enum.GetValues<Mode>())
            {
                (int note, int chordRoot) = Character[(int)mode];
                _keys[Index(new Key(tonic, mode))] = (new Key(tonic, mode).TriadOn(1), (tonic + note) % 12, new Chord((tonic + chordRoot) % 12, ChordQuality.Major));
            }
        }
    }

    public Chord Choose(int pitchClass, Key key, Chord? current)
    {
        (Chord tonic, int characterNote, Chord characterChord) = _keys[Index(key)];

        // In Locrian the character note is also the tonic triad's fifth; the character chord wins, as the table in
        // docs/harmony.md names it, or Locrian's character chord could never sound.
        if (pitchClass == characterNote)
        {
            return characterChord;
        }

        return tonic.Contains(pitchClass) || current != characterChord ? tonic : characterChord;
    }

    private static int Index(Key key)
    {
        return (key.Tonic * ModeCount) + (int)key.Mode;
    }
}
