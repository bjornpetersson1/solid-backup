namespace SolidBackup.Audio;

/// <summary>Fills each output buffer on the output's audio thread; it must not allocate, lock or do I/O.</summary>
public interface IAudioRenderer
{
    void Render(Span<float> output);
}
