using System.Reflection;
using System.Runtime.InteropServices;

using PaStream = PortAudioSharp.Stream;

namespace PortAudioSpike;

/// <summary>
/// PortAudio calls that PortAudioSharp2 does not wrap: the host API of a device and the
/// latencies PortAudio reports for an open stream.
/// </summary>
internal static class NativeInfo
{
    private const string Library = "portaudio";

    private static readonly FieldInfo StreamPointerField =
        typeof(PaStream).GetField("streamPtr", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("PortAudioSharp.Stream has no streamPtr field.");

    public static string HostApiName(int hostApi)
    {
        IntPtr info = Pa_GetHostApiInfo(hostApi);
        return info == IntPtr.Zero ? "?" : Marshal.PtrToStringUTF8(Marshal.PtrToStructure<HostApiInfo>(info).Name) ?? "?";
    }

    public static StreamInfo Info(PaStream stream)
    {
        IntPtr info = Pa_GetStreamInfo((IntPtr)StreamPointerField.GetValue(stream)!);
        return info == IntPtr.Zero
            ? throw new InvalidOperationException("Pa_GetStreamInfo returned null.")
            : Marshal.PtrToStructure<StreamInfo>(info);
    }

    [DllImport(Library)]
    private static extern IntPtr Pa_GetHostApiInfo(int hostApi);

    [DllImport(Library)]
    private static extern IntPtr Pa_GetStreamInfo(IntPtr stream);

    [StructLayout(LayoutKind.Sequential)]
    private struct HostApiInfo
    {
        public int StructVersion;
        public int Type;
        public IntPtr Name;
        public int DeviceCount;
        public int DefaultInputDevice;
        public int DefaultOutputDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StreamInfo
    {
        public int StructVersion;
        public double InputLatency;
        public double OutputLatency;
        public double SampleRate;
    }
}
