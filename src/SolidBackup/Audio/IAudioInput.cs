namespace SolidBackup.Audio;

/// <summary>Mono audio in, delivered as buffers of <see cref="BufferSize"/> frames.</summary>
public interface IAudioInput : IDisposable
{
    int SampleRate { get; }

    int BufferSize { get; }

    void Start(IAudioInputHandler handler);

    /// <summary>Stops delivering buffers; calling it again does nothing.</summary>
    void Stop();
}
