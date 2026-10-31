using PortAudioSharp;

using SolidBackup.Configuration;

using Pa = PortAudioSharp.PortAudio;

namespace SolidBackup.Audio.PortAudio;

/// <summary>
/// Owns one PortAudio session: <see cref="Open"/> initialises PortAudio and chooses the devices,
/// and <see cref="Dispose"/> closes the streams and terminates PortAudio, exactly once.
/// </summary>
public sealed class PortAudioDevices : IAudioDevices
{
    private readonly PortAudioInput _input;
    private readonly PortAudioOutput _output;
    private int _disposed;

    private PortAudioDevices(PortAudioDevice inputDevice, PortAudioDevice outputDevice, AppSettings settings)
    {
        _input = new PortAudioInput(inputDevice, settings.SampleRate, settings.BufferSize);
        _output = new PortAudioOutput(outputDevice, settings.SampleRate, settings.BufferSize);
    }

    public IAudioInput Input => _input;

    public IAudioOutput Output => _output;

    public AudioCounters Counters => new(
        _input.InputOverflows,
        _output.OutputUnderflows,
        _output.HandOverUnderruns,
        _output.HandOverOverruns,
        _output.DriftCorrections);

    public static PortAudioDevices Open(AppSettings settings)
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
            PortAudioDevice input = DefaultDevice(Pa.DefaultInputDevice, info => info.defaultLowInputLatency, "Det finns ingen ljudingång.");
            PortAudioDevice output = DefaultDevice(Pa.DefaultOutputDevice, info => info.defaultLowOutputLatency, "Det finns ingen ljudutgång.");
            return new PortAudioDevices(input, output, settings);
        }
        catch (PortAudioException exception)
        {
            Pa.Terminate();
            throw new AudioDeviceException($"Ljudenheterna kunde inte läsas ({Pa.GetErrorText(exception.ErrorCode)}).", exception);
        }
        catch
        {
            Pa.Terminate();
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
            _output.Dispose();
        }
        finally
        {
            try
            {
                _input.Dispose();
            }
            finally
            {
                Pa.Terminate();
            }
        }
    }

    private static PortAudioDevice DefaultDevice(int index, Func<DeviceInfo, double> lowLatency, string missingMessage)
    {
        if (index == Pa.NoDevice)
        {
            throw new AudioDeviceException(missingMessage);
        }

        DeviceInfo info = Pa.GetDeviceInfo(index);
        return new PortAudioDevice(index, lowLatency(info));
    }
}
