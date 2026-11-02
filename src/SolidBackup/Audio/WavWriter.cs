namespace SolidBackup.Audio;

/// <summary>Writes mono audio as a 16-bit PCM WAV file, clipping samples to ±1.</summary>
public static class WavWriter
{
    public static void WritePcm16(string path, int sampleRate, ReadOnlySpan<float> samples)
    {
        byte[] file = new byte[Pcm16Wav.HeaderSize + (samples.Length * Pcm16Wav.BytesPerSample)];
        Pcm16Wav.WriteHeader(file.AsSpan(0, Pcm16Wav.HeaderSize), sampleRate, channels: 1, (uint)(samples.Length * Pcm16Wav.BytesPerSample));
        Pcm16Wav.WritePcm16(samples, file.AsSpan(Pcm16Wav.HeaderSize));
        File.WriteAllBytes(path, file);
    }
}
