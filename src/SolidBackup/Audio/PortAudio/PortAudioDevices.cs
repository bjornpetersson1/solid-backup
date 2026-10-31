using SolidBackup.Audio.Devices;
using SolidBackup.Configuration;

namespace SolidBackup.Audio.PortAudio;

/// <summary>
/// The input and output chosen in the settings, on one <see cref="PortAudioSession"/>.
/// <see cref="Dispose"/> closes the streams and ends the session, exactly once.
/// </summary>
public sealed class PortAudioDevices : IAudioDevices
{
    private readonly PortAudioSession _session;
    private readonly PortAudioInput _input;
    private readonly PortAudioOutput _output;
    private int _disposed;

    private PortAudioDevices(PortAudioSession session, AudioDeviceInfo inputDevice, AudioDeviceInfo outputDevice, AppSettings settings)
    {
        _session = session;
        OutputDeviceName = outputDevice.Name;
        _input = new PortAudioInput(inputDevice, settings.SampleRate, settings.BufferSize);
        _output = new PortAudioOutput(outputDevice, settings.SampleRate, settings.BufferSize);
    }

    public IAudioInput Input => _input;

    public IAudioOutput Output => _output;

    public string OutputDeviceName { get; }

    public AudioCounters Counters => new(
        _input.InputOverflows,
        _output.OutputUnderflows,
        _output.HandOverUnderruns,
        _output.HandOverOverruns,
        _output.DriftCorrections);

    /// <summary>Opens PortAudio and chooses the devices named in the settings (empty means the system default).</summary>
    public static PortAudioDevices Open(AppSettings settings)
    {
        PortAudioSession session = PortAudioSession.Open();
        try
        {
            AudioDeviceInfo input = DeviceSelector.Select(session.Devices, settings.InputDevice, AudioDirection.Input);
            AudioDeviceInfo output = DeviceSelector.Select(session.Devices, settings.OutputDevice, AudioDirection.Output);
            return new PortAudioDevices(session, input, output, settings);
        }
        catch
        {
            session.Dispose();
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
                _session.Dispose();
            }
        }
    }
}
