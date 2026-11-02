using SolidBackup.Audio.Devices;

namespace SolidBackup.Tests.Audio.Devices;

/// <summary>A catalog of <paramref name="devices"/> (default <see cref="TestDevices.All"/>) that says whether it was closed.</summary>
internal sealed class FakeCatalog(IReadOnlyList<AudioDeviceInfo>? devices = null) : IAudioDeviceCatalog
{
    public IReadOnlyList<AudioDeviceInfo> Devices => devices ?? TestDevices.All;

    public bool Disposed { get; private set; }

    public void Dispose()
    {
        Disposed = true;
    }
}
