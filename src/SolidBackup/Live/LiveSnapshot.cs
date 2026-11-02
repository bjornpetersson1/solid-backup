using SolidBackup.Audio;
using SolidBackup.Configuration;

namespace SolidBackup.Live;

/// <summary>What the dashboard shows, read a few times a second off the audio threads.</summary>
/// <param name="Settings">The run's settings: the style, sound, rhythm, level features and gate threshold, fixed for the run.</param>
/// <param name="Recording">The recording so far, or null when not recording.</param>
public sealed record LiveSnapshot(
    LiveState State, AppSettings Settings, LiveLatency Latency, AudioCounters Counters, RecordingStatus? Recording);

/// <summary>How long the recording is so far and how many of its buffers were dropped.</summary>
public readonly record struct RecordingStatus(double Seconds, long Overruns);
