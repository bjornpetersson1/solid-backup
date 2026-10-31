using PortAudioSharp;

using SolidBackup.Audio.Devices;

using Pa = PortAudioSharp.PortAudio;

namespace SolidBackup.Audio.PortAudio;

/// <summary>
/// One initialised PortAudio: <see cref="Open"/> initialises it and reads the devices, and
/// <see cref="Dispose"/> terminates it, exactly once.
/// </summary>
public sealed class PortAudioSession : IAudioDeviceCatalog
{
    private int _disposed;

    private PortAudioSession(IReadOnlyList<AudioDeviceInfo> devices)
    {
        Devices = devices;
    }

    public IReadOnlyList<AudioDeviceInfo> Devices { get; }

    public static PortAudioSession Open()
    {
        try
        {
            Pa.Initialize();
        }
        catch (Exception exception) when (exception is PortAudioException or DllNotFoundException
            or EntryPointNotFoundException or TypeInitializationException)
        {
            throw new AudioDeviceException($"Ljudsystemet PortAudio kunde inte startas ({exception.Message}).", exception);
        }

        try
        {
            return new PortAudioSession(ReadDevices());
        }
        catch (Exception exception)
        {
            Pa.Terminate();
            throw exception is PortAudioException portAudioError
                ? new AudioDeviceException($"Ljudenheterna kunde inte läsas ({Pa.GetErrorText(portAudioError.ErrorCode)}).", exception)
                : exception;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Pa.Terminate();
        }
    }

    private static AudioDeviceInfo[] ReadDevices()
    {
        int defaultInput = Pa.DefaultInputDevice;
        int defaultOutput = Pa.DefaultOutputDevice;
        return Enumerable.Range(0, Pa.DeviceCount)
            .Select(index =>
            {
                DeviceInfo info = Pa.GetDeviceInfo(index);
                return new AudioDeviceInfo(
                    index,
                    info.name,
                    info.maxInputChannels,
                    info.maxOutputChannels,
                    info.defaultLowInputLatency,
                    info.defaultLowOutputLatency,
                    index == defaultInput,
                    index == defaultOutput);
            })
            .ToArray();
    }
}
