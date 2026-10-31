namespace SolidBackup.Audio;

/// <summary>Receives each input buffer on the audio thread; it must not allocate, lock or do I/O.</summary>
public interface IAudioInputHandler
{
    void OnBuffer(ReadOnlySpan<float> samples);
}
