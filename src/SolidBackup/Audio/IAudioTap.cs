namespace SolidBackup.Audio;

/// <summary>Sees each buffer the pipeline handles, its input and what went to the output, on the audio thread; it must not allocate, lock or wait.</summary>
public interface IAudioTap
{
    void OnBuffer(ReadOnlySpan<float> input, ReadOnlySpan<float> output);
}
