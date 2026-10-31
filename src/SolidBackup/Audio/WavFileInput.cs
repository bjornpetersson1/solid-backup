namespace SolidBackup.Audio;

/// <summary>
/// Plays a WAV file as input: <see cref="Start"/> delivers every buffer synchronously, as fast
/// as possible, and pads the last one with zeros. The file is read once, up front.
/// </summary>
public sealed class WavFileInput : IAudioInput
{
    private readonly MemoryAudioInput _input;

    public WavFileInput(string path, int sampleRate, int bufferSize)
    {
        WavAudio audio = WavReader.Read(path);
        if (audio.SampleRate != sampleRate)
        {
            throw new InvalidDataException(
                $"WAV-filen {path} har samplingsfrekvensen {audio.SampleRate} Hz men {sampleRate} Hz krävs; den omsamplas inte.");
        }

        _input = new MemoryAudioInput(audio.Samples, sampleRate, bufferSize);
    }

    public int SampleRate => _input.SampleRate;

    public int BufferSize => _input.BufferSize;

    public void Start(IAudioInputHandler handler)
    {
        _input.Start(handler);
    }

    public void Stop()
    {
        _input.Stop();
    }

    public void Dispose()
    {
        _input.Dispose();
    }
}
