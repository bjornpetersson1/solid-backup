namespace SolidBackup.Configuration;

public sealed record AppSettings
{
    public required string LowestNote { get; init; }

    public required string HighestNote { get; init; }

    public required int BufferSize { get; init; }

    public required int SampleRate { get; init; }

    /// <summary>The input device's name; empty means the system default.</summary>
    public required string InputDevice { get; init; }

    /// <summary>The output device's name; empty means the system default.</summary>
    public required string OutputDevice { get; init; }

    /// <summary>The YIN confidence (0–1) a pitch needs before it is reported as a note.</summary>
    public required double PitchConfidenceThreshold { get; init; }

    public required double GateThresholdDbfs { get; init; }

    /// <summary>How far below the gate threshold the level must fall before the gate may close.</summary>
    public required double GateHysteresisDb { get; init; }

    /// <summary>How long the level must stay below the close threshold before the gate closes.</summary>
    public required double GateHoldMs { get; init; }

    /// <summary>The rise in level, within <c>OnsetDetector.RiseMs</c>, that counts as an onset.</summary>
    public required double OnsetThresholdDb { get; init; }

    /// <summary>The shortest time between two onsets.</summary>
    public required double OnsetMinIntervalMs { get; init; }

    public required int LatencyTargetMs { get; init; }

    /// <summary>The harmony style's name, e.g. <c>Church</c>; see <c>docs/harmony.md</c>.</summary>
    public required string HarmonyStyle { get; init; }

    /// <summary>The tonic, e.g. <c>C</c>, <c>F#</c> or <c>Bb</c>, or <c>auto</c> to estimate the key.</summary>
    public required string Key { get; init; }

    /// <summary>The mode's name, e.g. <c>Ionian</c>; ignored with <c>auto</c>.</summary>
    public required string Mode { get; init; }

    /// <summary>While singing, how long a new note must last before it may change the chord.</summary>
    public required double MinNoteForChangeMs { get; init; }

    /// <summary>While singing, the shortest time between two chord changes.</summary>
    public required double MinChordDurationMs { get; init; }

    /// <summary>
    /// While singing, the most chord changes per bar, each on an allowed beat of the tempo grid: <c>Off</c>, <c>1</c>,
    /// <c>2</c> or <c>Beat</c>; see <c>docs/harmony.md</c>.
    /// </summary>
    public required string ChordChangesPerBar { get; init; }

    /// <summary>How easily a new note changes the chord: <c>Low</c>, <c>Medium</c> or <c>High</c>, a factor of 2, 1 or 0.5 on both hold times.</summary>
    public required string ChordChangeSensitivity { get; init; }

    /// <summary>After a silence this long, the previous chord is forgotten.</summary>
    public required double PhraseResetMs { get; init; }

    /// <summary>With <c>auto</c>, the longest a modulation may take before the new key is reported.</summary>
    public required double KeyChangeSeconds { get; init; }

    /// <summary>With <c>auto</c>, how many seconds of notes are needed before the first key.</summary>
    public required double KeyFirstSeconds { get; init; }

    /// <summary>The synthesizer's sound: <c>Pad</c>, <c>Organ</c> or <c>Pluck</c>.</summary>
    public required string Sound { get; init; }

    /// <summary>Level feature: notes reach the harmony, and the chord sounds, only while the noise gate is open.</summary>
    public required bool GateOn { get; init; }

    /// <summary>Level feature: the chord's strength follows the input level.</summary>
    public required bool DynamicsOn { get; init; }

    /// <summary>Level feature: soft singing gives a sparse chord, loud singing a full one.</summary>
    public required bool ChordCharacterOn { get; init; }

    /// <summary>Level feature: an articulated note changes the chord without the hold times and strikes it anew.</summary>
    public required bool OnsetsOn { get; init; }

    /// <summary>Dynamics: the level that plays 24 dB below full strength, and below.</summary>
    public required double DynamicsQuietDbfs { get; init; }

    /// <summary>Dynamics: the level that plays at full strength, and above.</summary>
    public required double DynamicsLoudDbfs { get; init; }

    /// <summary>Chord character: below this level the chord is sparse.</summary>
    public required double CharacterSparseBelowDbfs { get; init; }

    /// <summary>Chord character: above this level the chord is full.</summary>
    public required double CharacterFullAboveDbfs { get; init; }

    /// <summary>The rhythm pattern: <c>Held</c>, <c>Arpeggio</c> or <c>Strum</c>; see <c>docs/rhythm.md</c>.</summary>
    public required string RhythmPattern { get; init; }

    /// <summary>The tempo in beats per minute, 40–240.</summary>
    public required int Bpm { get; init; }

    /// <summary>The time signature: <c>4/4</c> or <c>3/4</c>.</summary>
    public required string TimeSignature { get; init; }

    /// <summary>Where recordings go: a folder relative to the current directory, or an absolute path.</summary>
    public required string RecordingFolder { get; init; }

    /// <summary>Whether live mode started from the menu records the session.</summary>
    public required bool RecordOn { get; init; }
}
