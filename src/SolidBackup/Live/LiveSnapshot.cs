using SolidBackup.Audio;

namespace SolidBackup.Live;

/// <summary>What the dashboard shows, read a few times a second off the audio threads.</summary>
/// <param name="FeaturesOn">The level features that are on, by name.</param>
public sealed record LiveSnapshot(
    LiveState State, IReadOnlyList<string> FeaturesOn, string RhythmPattern, LiveLatency Latency, AudioCounters Counters);
