namespace SolidBackup.Audio;

/// <summary>
/// Plays samples held in memory as input: <see cref="Start"/> delivers every buffer synchronously,
/// as fast as possible, and pads the last one with zeros. It can tell how far it has come and stop once cancelled, so
/// a long offline run (E8-4) shows its progress and can be left.
/// </summary>
public sealed class MemoryAudioInput : IAudioInput
{
    private readonly float[] _samples;
    private readonly float[] _buffer;
    private readonly Action<double>? _progress;
    private readonly CancellationToken _cancellation;
    private bool _stopRequested;

    /// <param name="progress">Told how far it has come, from 0 to 1, after each buffer.</param>
    /// <param name="cancellation">Stops the delivery before the next buffer.</param>
    public MemoryAudioInput(float[] samples, int sampleRate, int bufferSize, Action<double>? progress = null, CancellationToken cancellation = default)
    {
        _samples = samples;
        _progress = progress;
        _cancellation = cancellation;
        _buffer = new float[bufferSize];
        SampleRate = sampleRate;
    }

    public int SampleRate { get; }

    public int BufferSize => _buffer.Length;

    public void Start(IAudioInputHandler handler)
    {
        _stopRequested = false;
        for (int offset = 0; offset < _samples.Length && !_stopRequested && !_cancellation.IsCancellationRequested; offset += BufferSize)
        {
            int count = Math.Min(BufferSize, _samples.Length - offset);
            if (count == BufferSize)
            {
                handler.OnBuffer(_samples.AsSpan(offset, count));
            }
            else
            {
                _samples.AsSpan(offset, count).CopyTo(_buffer);
                _buffer.AsSpan(count).Clear();
                handler.OnBuffer(_buffer);
            }

            _progress?.Invoke((double)(offset + count) / _samples.Length);
        }
    }

    public void Stop()
    {
        _stopRequested = true;
    }

    public void Dispose()
    {
        Stop();
    }
}
