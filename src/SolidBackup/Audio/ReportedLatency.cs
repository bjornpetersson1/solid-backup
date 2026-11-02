namespace SolidBackup.Audio;

/// <summary>The input and output latencies the audio system reports for the open streams, in milliseconds.</summary>
public readonly record struct ReportedLatency(double InputMs, double OutputMs);
