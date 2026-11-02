using PortAudioSharp;

using SolidBackup.Audio.Devices;

using Pa = PortAudioSharp.PortAudio;
using PaStream = PortAudioSharp.Stream;

namespace SolidBackup.Audio.PortAudio;

/// <summary>Opens, starts and closes PortAudio streams, turning PortAudio's errors into Swedish ones.</summary>
internal static class PortAudioStreams
{
    public static PaStream Open(
        StreamParameters? input, StreamParameters? output, int sampleRate, int bufferSize, PaStream.Callback callback, string deviceName)
    {
        PaStream? stream = null;
        try
        {
            stream = new PaStream(input, output, sampleRate, (uint)bufferSize, StreamFlags.ClipOff, callback, null);
            stream.Start();
            return stream;
        }
        catch (PortAudioException exception)
        {
            stream?.Dispose();
            throw new AudioDeviceException($"{deviceName} kunde inte öppnas ({Pa.GetErrorText(exception.ErrorCode)}).", exception);
        }
    }

    public static StreamParameters Parameters(AudioDeviceInfo device, AudioDirection direction, int channels)
    {
        return new StreamParameters
        {
            device = device.Index,
            channelCount = channels,
            sampleFormat = SampleFormat.Float32,
            suggestedLatency = device.LowLatency(direction),
            hostApiSpecificStreamInfo = IntPtr.Zero,
        };
    }

    /// <summary>Stops and closes the stream, if there is one; calling it again does nothing.</summary>
    public static void Close(ref PaStream? stream, string deviceName)
    {
        if (stream is null)
        {
            return;
        }

        PaStream closing = stream;
        stream = null;
        try
        {
            closing.Stop();
        }
        catch (PortAudioException exception)
        {
            throw new AudioDeviceException($"{deviceName} kunde inte stängas ({Pa.GetErrorText(exception.ErrorCode)}).", exception);
        }
        finally
        {
            closing.Dispose();
        }
    }

    /// <summary>Stereo, or mono on a one-channel device: the mono signal goes to every channel.</summary>
    public static int OutputChannels(AudioDeviceInfo device)
    {
        return Math.Min(2, device.OutputChannels);
    }

    /// <summary>Copies a mono buffer to every channel of an interleaved one and clears the frames beyond it.</summary>
    public static void FanOut(ReadOnlySpan<float> mono, Span<float> interleaved, int channels)
    {
        for (int frame = 0; frame < mono.Length; frame++)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                interleaved[(frame * channels) + channel] = mono[frame];
            }
        }

        interleaved[(mono.Length * channels)..].Clear();
    }
}
