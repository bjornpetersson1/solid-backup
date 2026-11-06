namespace SolidBackup.Audio;

/// <summary>
/// Plays audio that is already rendered, such as chords from the synthesizer, on an output device. Unlike
/// <see cref="IAudioOutput"/>, no input drives it: the output's own callback takes the samples as it needs them.
/// </summary>
public interface IAudioPlayback : IDisposable
{
    /// <summary>The output device's name, for the speaker check.</summary>
    string OutputDeviceName { get; }

    /// <summary>True once every sample given to <see cref="Start"/> has been played.</summary>
    bool IsFinished { get; }

    /// <summary>Output underflows reported by the device while playing.</summary>
    long Underflows { get; }

    /// <summary>The sample played next, from 0 to the samples' length.</summary>
    int Position { get; }

    /// <summary>While true, silence is played and the position stays where it is.</summary>
    bool Paused { get; set; }

    void Start(float[] samples);

    /// <summary>Plays on from <paramref name="position"/>, clamped to the samples.</summary>
    void Seek(int position);

    /// <summary>Stops playing; calling it again does nothing.</summary>
    void Stop();
}
