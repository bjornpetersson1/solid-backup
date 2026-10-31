namespace SolidBackup.Audio;

/// <summary>An opened input and output; disposing closes both and releases the audio system.</summary>
public interface IAudioDevices : IDisposable
{
    IAudioInput Input { get; }

    IAudioOutput Output { get; }

    AudioCounters Counters { get; }
}
