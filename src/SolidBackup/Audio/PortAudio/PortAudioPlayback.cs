using PortAudioSharp;

using SolidBackup.Audio.Devices;
using SolidBackup.Configuration;

using PaStream = PortAudioSharp.Stream;

namespace SolidBackup.Audio.PortAudio;

/// <summary>
/// Plays rendered mono audio on every channel of the output device chosen in the settings. The stream's callback
/// reads straight from the samples, so there is one clock and nothing to hand over or correct for drift.
/// </summary>
public sealed class PortAudioPlayback : IAudioPlayback
{
    private readonly PortAudioSession _session;
    private readonly AudioDeviceInfo _device;
    private readonly int _channels;
    private readonly int _sampleRate;
    private readonly int _bufferSize;
    private readonly int _tailFrames;
    private float[] _samples = [];
    private PaStream? _stream;
    private PlaybackCursor _cursor = new();
    private long _underflows;

    private PortAudioPlayback(PortAudioSession session, AudioDeviceInfo device, AppSettings settings)
    {
        _session = session;
        _device = device;
        _channels = PortAudioStreams.OutputChannels(device);
        _sampleRate = settings.SampleRate;
        _bufferSize = settings.BufferSize;

        // Silence played after the samples, longer than the device's output latency, so the last ones are heard before the stream stops.
        _tailFrames = settings.SampleRate / 10;
    }

    public string OutputDeviceName => _device.Name;

    /// <summary>True once the samples, and a short silence after them, have been handed to the device, so stopping cuts none of the sound.</summary>
    public bool IsFinished => _cursor.Position >= _samples.Length + _tailFrames;

    public long Underflows => Interlocked.Read(ref _underflows);

    public int Position => Math.Min(_cursor.Position, _samples.Length);

    public bool Paused
    {
        get => _cursor.Paused;
        set => _cursor.Paused = value;
    }

    public static PortAudioPlayback Open(AppSettings settings)
    {
        PortAudioSession session = PortAudioSession.Open();
        try
        {
            return new PortAudioPlayback(session, DeviceSelector.Select(session.Devices, settings.OutputDevice, AudioDirection.Output), settings);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public void Start(float[] samples)
    {
        if (_stream is not null)
        {
            throw new InvalidOperationException("The playback is already started.");
        }

        _samples = samples;
        _cursor = new PlaybackCursor();
        _stream = PortAudioStreams.Open(
            null, PortAudioStreams.Parameters(_device, AudioDirection.Output, _channels), _sampleRate, _bufferSize, OnAudio, PortAudioOutput.DeviceName);
    }

    public void Seek(int position)
    {
        _cursor.Seek(position, _samples.Length);
    }

    public void Stop()
    {
        PortAudioStreams.Close(ref _stream, PortAudioOutput.DeviceName);
    }

    public void Dispose()
    {
        try
        {
            Stop();
        }
        finally
        {
            _session.Dispose();
        }
    }

    private unsafe StreamCallbackResult OnAudio(
        IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        if ((statusFlags & StreamCallbackFlags.OutputUnderflow) != 0)
        {
            Interlocked.Increment(ref _underflows);
        }

        _cursor.Fill(_samples, new Span<float>((void*)output, (int)frameCount * _channels), _channels);
        return StreamCallbackResult.Continue;
    }
}
