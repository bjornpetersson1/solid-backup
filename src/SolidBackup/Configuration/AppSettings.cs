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

    /// <summary>After a silence this long, the previous chord is forgotten.</summary>
    public required double PhraseResetMs { get; init; }

    /// <summary>With <c>auto</c>, the longest a modulation may take before the new key is reported.</summary>
    public required double KeyChangeSeconds { get; init; }

    /// <summary>With <c>auto</c>, how many seconds of notes are needed before the first key.</summary>
    public required double KeyFirstSeconds { get; init; }
}
