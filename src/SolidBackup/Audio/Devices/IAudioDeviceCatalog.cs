namespace SolidBackup.Audio.Devices;

/// <summary>The devices of an opened audio system; disposing releases the audio system.</summary>
public interface IAudioDeviceCatalog : IDisposable
{
    IReadOnlyList<AudioDeviceInfo> Devices { get; }
}
