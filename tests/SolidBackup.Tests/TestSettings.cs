using SolidBackup.Configuration;

namespace SolidBackup.Tests;

/// <summary>Settings equal to the bundled defaults; vary them with <c>with</c>.</summary>
internal static class TestSettings
{
    public static readonly AppSettings Default = new()
    {
        LowestNote = "E2",
        HighestNote = "C7",
        BufferSize = 64,
        SampleRate = 48_000,
        InputDevice = "",
        OutputDevice = "",
        PitchConfidenceThreshold = 0.8,
        GateThresholdDbfs = -42,
        GateHysteresisDb = 3,
        GateHoldMs = 20,
        OnsetThresholdDb = 15,
        OnsetMinIntervalMs = 100,
        LatencyTargetMs = 50,
        HarmonyStyle = "Church",
        Key = "auto",
        Mode = "Ionian",
        MinNoteForChangeMs = 120,
        MinChordDurationMs = 250,
        ChordChangesPerBar = "2",
        ChordChangeSensitivity = "Medium",
        PhraseResetMs = 2000,
        KeyChangeSeconds = 4,
        KeyFirstSeconds = 2,
        Sound = "Pad",
        GateOn = true,
        DynamicsOn = true,
        ChordCharacterOn = true,
        OnsetsOn = true,
        DynamicsQuietDbfs = -42,
        DynamicsLoudDbfs = -12,
        CharacterSparseBelowDbfs = -30,
        CharacterFullAboveDbfs = -18,
        RecordingFolder = "recordings",
        RecordOn = false,
        RhythmPattern = "Held",
        Bpm = 90,
        TimeSignature = "4/4",
    };

    /// <summary>
    /// The settings the examples in <c>docs/harmony.md</c> run with, except the change-rate examples: the defaults
    /// with <c>ChordChangesPerBar</c> <c>Off</c>.
    /// </summary>
    public static readonly AppSettings Examples = Default with { ChordChangesPerBar = "Off" };
}
