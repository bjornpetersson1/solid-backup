using SolidBackup.Audio;

namespace SolidBackup.Measurement;

/// <summary>A corpus clip with its reference; <see cref="Path"/> is set for a recording.</summary>
public sealed record CorpusClip(string Name, int SampleRate, float[] Samples, ClipReference Reference, string? Path = null)
{
    /// <summary>Opens the clip as audio input: the WAV file for a recording, the samples for a generated clip.</summary>
    public IAudioInput OpenInput(int bufferSize)
    {
        return Path is null
            ? new MemoryAudioInput(Samples, SampleRate, bufferSize)
            : new WavFileInput(Path, SampleRate, bufferSize);
    }
}
