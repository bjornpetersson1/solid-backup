namespace SolidBackup.Audio;

/// <summary>Keeps everything written to it in a buffer allocated up front.</summary>
public sealed class MemoryAudioOutput(int sampleRate, int bufferSize, int capacityFrames) : IAudioOutput
{
    private readonly float[] _samples = new float[capacityFrames];
    private int _length;
    private bool _started;

    public int SampleRate => sampleRate;

    public int BufferSize => bufferSize;

    public ReadOnlySpan<float> Samples => _samples.AsSpan(0, _length);

    public void Start()
    {
        _started = true;
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        if (!_started)
        {
            throw new InvalidOperationException("The memory output is not started.");
        }

        if (_length + samples.Length > _samples.Length)
        {
            throw new InvalidOperationException($"The memory output holds only {_samples.Length} frames.");
        }

        samples.CopyTo(_samples.AsSpan(_length));
        _length += samples.Length;
    }

    public void Stop()
    {
        _started = false;
    }

    public void Dispose()
    {
        Stop();
    }
}
