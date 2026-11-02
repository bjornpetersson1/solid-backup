namespace SolidBackup.Audio;

/// <summary>
/// Connects an input to an output through a processor. Each input buffer is processed into a
/// buffer allocated up front and written to the output on the input's audio thread.
/// </summary>
public sealed class AudioPipeline : IAudioInputHandler
{
    private readonly IAudioInput _input;
    private readonly IAudioOutput _output;
    private readonly IAudioProcessor _processor;
    private readonly float[] _processed;
    private readonly IAudioTap? _tap;

    /// <param name="tap">Sees each input buffer and what went to the output, e.g. a recorder.</param>
    public AudioPipeline(IAudioInput input, IAudioOutput output, IAudioProcessor processor, IAudioTap? tap = null)
    {
        if (input.SampleRate != output.SampleRate || input.BufferSize != output.BufferSize)
        {
            throw new ArgumentException(
                $"The input ({input.SampleRate} Hz, {input.BufferSize} frames) and the output "
                + $"({output.SampleRate} Hz, {output.BufferSize} frames) must have the same format.",
                nameof(output));
        }

        _input = input;
        _output = output;
        _processor = processor;
        _tap = tap;
        _processed = new float[input.BufferSize];
    }

    public void Start()
    {
        AudioStreams.Start(_input, this, _output);
    }

    public void Stop()
    {
        AudioStreams.Stop(_input, _output);
    }

    public void OnBuffer(ReadOnlySpan<float> samples)
    {
        Span<float> processed = _processed.AsSpan(0, Math.Min(samples.Length, _processed.Length));
        _processor.Process(samples[..processed.Length], processed);
        _output.Write(processed);
        _tap?.OnBuffer(samples[..processed.Length], processed);
    }
}
