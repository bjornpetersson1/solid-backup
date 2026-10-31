namespace SolidBackup.Audio;

/// <summary>Decoded WAV audio, mixed down to mono.</summary>
public sealed record WavAudio(int SampleRate, float[] Samples);
