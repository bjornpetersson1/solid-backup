namespace SolidBackup.Audio;

/// <summary>Turns one input buffer into one output buffer of the same length, without allocating.</summary>
public interface IAudioProcessor
{
    void Process(ReadOnlySpan<float> input, Span<float> output);
}
