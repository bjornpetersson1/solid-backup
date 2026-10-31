namespace SolidBackup.Audio;

/// <summary>
/// Plays a WAV file as input: <see cref="Start"/> delivers every buffer synchronously, as fast
/// as possible, and pads the last one with zeros. The file is read once, up front.
/// </summary>
public sealed class WavFileInput : IAudioInput
{
    private readonly float[] _samples;
    private readonly float[] _buffer;
    private bool _stopRequested;

    public WavFileInput(string path, int sampleRate, int bufferSize)
    {
        WavAudio audio = WavReader.Read(path);
        if (audio.SampleRate != sampleRate)
        {
            throw new InvalidDataException(
                $"WAV-filen {path} har samplingsfrekvensen {audio.SampleRate} Hz men {sampleRate} Hz krävs; den omsamplas inte.");
        }

        _samples = audio.Samples;
        _buffer = new float[bufferSize];
        SampleRate = sampleRate;
    }

    public int SampleRate { get; }

    public int BufferSize => _buffer.Length;

    public void Start(IAudioInputHandler handler)
    {
        _stopRequested = false;
        for (int offset = 0; offset < _samples.Length && !_stopRequested; offset += BufferSize)
        {
            int count = Math.Min(BufferSize, _samples.Length - offset);
            if (count == BufferSize)
            {
                handler.OnBuffer(_samples.AsSpan(offset, count));
                continue;
            }

            _samples.AsSpan(offset, count).CopyTo(_buffer);
            _buffer.AsSpan(count).Clear();
            handler.OnBuffer(_buffer);
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
