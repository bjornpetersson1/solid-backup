namespace SolidBackup.Audio;

/// <summary>Mono audio out; <see cref="Write"/> is called from the input's audio thread.</summary>
public interface IAudioOutput : IDisposable
{
    int SampleRate { get; }

    int BufferSize { get; }

    void Start();

    void Write(ReadOnlySpan<float> samples);

    /// <summary>Stops playing; calling it again does nothing.</summary>
    void Stop();
}
