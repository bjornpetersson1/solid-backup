namespace SolidBackup.Audio.Devices;

public interface IOutputRouteSource
{
    /// <summary>The route of the named output device, or null when it cannot be read.</summary>
    OutputRoute? Route(string deviceName);
}
