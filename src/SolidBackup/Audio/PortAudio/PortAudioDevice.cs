namespace SolidBackup.Audio.PortAudio;

/// <summary>A PortAudio device chosen for one direction, with the latency to open it at.</summary>
public sealed record PortAudioDevice(int Index, double SuggestedLatency);
