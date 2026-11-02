namespace SolidBackup.Audio;

/// <summary>
/// Keeps everything written to it in a buffer allocated up front. Started with a renderer, it renders one buffer
/// each time <see cref="Pull"/> is called, so a test decides when the output's callback runs.
/// </summary>
public sealed class MemoryAudioOutput(int sampleRate, int bufferSize, int capacityFrames) : IAudioOutput
{
    private readonly float[] _samples = new float[capacityFrames];
    private IAudioRenderer? _renderer;
    private int _length;
    private bool _started;

    public int SampleRate => sampleRate;

    public int BufferSize => bufferSize;

    public ReadOnlySpan<float> Samples => _samples.AsSpan(0, _length);

    public void Start(IAudioRenderer? renderer = null)
    {
        _renderer = renderer;
        _started = true;
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        CheckRoom(samples.Length);
        samples.CopyTo(_samples.AsSpan(_length));
        _length += samples.Length;
    }

    /// <summary>Renders the next buffer from the renderer, as the output's callback would.</summary>
    public void Pull()
    {
        if (_renderer is null)
        {
            throw new InvalidOperationException("The memory output was not started with a renderer.");
        }

        CheckRoom(bufferSize);
        _renderer.Render(_samples.AsSpan(_length, bufferSize));
        _length += bufferSize;
    }

    public void Stop()
    {
        _started = false;
    }

    public void Dispose()
    {
        Stop();
    }

    private void CheckRoom(int frames)
    {
        if (!_started)
        {
            throw new InvalidOperationException("The memory output is not started.");
        }

        if (_length + frames > _samples.Length)
        {
            throw new InvalidOperationException($"The memory output holds only {_samples.Length} frames.");
        }
    }
}
