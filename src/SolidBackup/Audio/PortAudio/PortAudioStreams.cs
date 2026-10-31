using PortAudioSharp;

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

    public static StreamParameters Parameters(PortAudioDevice device, int channels)
    {
        return new StreamParameters
        {
            device = device.Index,
            channelCount = channels,
            sampleFormat = SampleFormat.Float32,
            suggestedLatency = device.SuggestedLatency,
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
}
