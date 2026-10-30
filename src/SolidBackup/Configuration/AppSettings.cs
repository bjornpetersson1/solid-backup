namespace SolidBackup.Configuration;

public sealed record AppSettings
{
    public required string LowestNote { get; init; }

    public required string HighestNote { get; init; }

    public required int BufferSize { get; init; }

    public required double GateThresholdDbfs { get; init; }

    public required int LatencyTargetMs { get; init; }
}
