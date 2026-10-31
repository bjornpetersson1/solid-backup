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

    public required double GateThresholdDbfs { get; init; }

    public required int LatencyTargetMs { get; init; }
}
