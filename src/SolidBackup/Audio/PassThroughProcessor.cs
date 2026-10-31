namespace SolidBackup.Audio;

public sealed class PassThroughProcessor : IAudioProcessor
{
    public void Process(ReadOnlySpan<float> input, Span<float> output)
    {
        input.CopyTo(output);
    }
}
