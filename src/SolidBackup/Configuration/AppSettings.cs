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
}
