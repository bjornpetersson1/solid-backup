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
    };
}
