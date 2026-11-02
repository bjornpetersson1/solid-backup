using System.Reflection;
using System.Runtime.InteropServices;

using PaStream = PortAudioSharp.Stream;

namespace SolidBackup.Audio.PortAudio;

/// <summary>The latencies PortAudio reports for an open stream, which <c>PortAudioSharp2</c> does not wrap (as in the E2-1 spike).</summary>
internal static class PortAudioStreamInfo
{
    private const string Library = "portaudio";

    // Null if a later binding renames the field; the latency is then reported as zero rather than failing the stream.
    private static readonly FieldInfo? StreamPointerField =
        typeof(PaStream).GetField("streamPtr", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>The input and output latency of the stream in milliseconds, or zero when PortAudio does not say.</summary>
    public static ReportedLatency Latency(PaStream stream)
    {
        if (StreamPointerField?.GetValue(stream) is not IntPtr pointer)
        {
            return default;
        }

        IntPtr info = Pa_GetStreamInfo(pointer);
        if (info == IntPtr.Zero)
        {
            return default;
        }

        StreamInfo read = Marshal.PtrToStructure<StreamInfo>(info);
        return new ReportedLatency(read.InputLatency * 1000, read.OutputLatency * 1000);
    }

    [DllImport(Library)]
    private static extern IntPtr Pa_GetStreamInfo(IntPtr stream);

    [StructLayout(LayoutKind.Sequential)]
    private struct StreamInfo
    {
        public int StructVersion;
        public double InputLatency;
        public double OutputLatency;
        public double SampleRate;
    }
}
