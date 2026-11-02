namespace SolidBackup.Audio;

/// <summary>
/// Mono audio out, in one of two ways: started without a renderer, it plays what <see cref="Write"/> hands it from
/// the input's audio thread; started with one, it asks the renderer for each buffer on its own thread.
/// </summary>
public interface IAudioOutput : IDisposable
{
    int SampleRate { get; }

    int BufferSize { get; }

    void Start(IAudioRenderer? renderer = null);

    void Write(ReadOnlySpan<float> samples);

    /// <summary>Stops playing; calling it again does nothing.</summary>
    void Stop();
}
