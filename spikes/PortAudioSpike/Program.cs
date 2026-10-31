using System.Globalization;
using System.Runtime.InteropServices;

using PortAudioSharp;

using PaStream = PortAudioSharp.Stream;

namespace PortAudioSpike;

/// <summary>
/// E2-1 spike: checks PortAudioSharp2 on macOS. Not part of the solution; see the README's
/// "Audio on macOS (E2-1 findings)" section for the results.
/// </summary>
public static class Program
{
    public const int OutputChannels = 2;

    private static readonly int[] BufferSizes = [32, 64, 128, 256, 512];
    private static double sampleRate = 48_000;

    public static int Main(string[] args)
    {
        string mode = args.FirstOrDefault() ?? "";
        if (args.Contains("--rate"))
        {
            if (!double.TryParse(OptionValue(args, "--rate"), NumberStyles.Float, CultureInfo.InvariantCulture, out sampleRate) || sampleRate <= 0)
            {
                return Usage();
            }
        }

        PortAudio.Initialize();
        try
        {
            switch (mode)
            {
                case "devices":
                    ListDevices();
                    return 0;
                case "latency":
                    MeasureLatencies();
                    return 0;
                case "stability" when args.Length >= 3
                    && uint.TryParse(args[1], out uint frames) && frames > 0
                    && int.TryParse(args[2], out int seconds) && seconds > 0:
                    bool mute = args.Contains("--mute");
                    if (args.Contains("--separate"))
                    {
                        RunSeparateStability(frames, seconds, mute);
                    }
                    else
                    {
                        RunStability(frames, seconds, mute);
                    }

                    return 0;
                default:
                    return Usage();
            }
        }
        finally
        {
            PortAudio.Terminate();
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: devices | latency | stability <frames> <seconds> [--separate] [--mute] [--rate <Hz>]");
        return 1;
    }

    private static void ListDevices()
    {
        Console.WriteLine($"PortAudio {PortAudio.VersionInfo.versionText}, runtime {RuntimeInformation.RuntimeIdentifier}");
        int deviceCount = PortAudio.DeviceCount;
        int defaultInput = PortAudio.DefaultInputDevice;
        int defaultOutput = PortAudio.DefaultOutputDevice;
        for (int device = 0; device < deviceCount; device++)
        {
            DeviceInfo info = PortAudio.GetDeviceInfo(device);
            string defaults = (device == defaultInput ? " [default in]" : "")
                + (device == defaultOutput ? " [default out]" : "");
            Console.WriteLine(
                $"{device}: {info.name} ({NativeInfo.HostApiName(info.hostApi)}){defaults}\n"
                + $"   in {info.maxInputChannels} ch, out {info.maxOutputChannels} ch, {info.defaultSampleRate} Hz, "
                + $"latency in low/high {Ms(info.defaultLowInputLatency)}/{Ms(info.defaultHighInputLatency)}, "
                + $"out low/high {Ms(info.defaultLowOutputLatency)}/{Ms(info.defaultHighOutputLatency)}");
        }
    }

    private static void MeasureLatencies()
    {
        StreamParameters input = InputParameters();
        StreamParameters output = OutputParameters();
        Console.WriteLine($"Sample rate: {sampleRate} Hz");
        Console.WriteLine($"Default input: {PortAudio.GetDeviceInfo(input.device).name}");
        Console.WriteLine($"Default output: {PortAudio.GetDeviceInfo(output.device).name}");
        Console.WriteLine("frames | input-only in | output-only out | duplex in | duplex out | duplex sum (ms)");
        foreach (uint frames in BufferSizes)
        {
            NativeInfo.StreamInfo inputOnly = OpenAndReport(input, null, frames);
            NativeInfo.StreamInfo outputOnly = OpenAndReport(null, output, frames);
            NativeInfo.StreamInfo duplex = OpenAndReport(input, output, frames);
            Console.WriteLine(
                $"{frames,6} | {Ms(inputOnly.InputLatency),13} | {Ms(outputOnly.OutputLatency),15} | "
                + $"{Ms(duplex.InputLatency),9} | {Ms(duplex.OutputLatency),10} | {Ms(duplex.InputLatency + duplex.OutputLatency),15}");
        }
    }

    // Reads the latencies while the stream runs, since CoreAudio settles them only once it has started.
    private static NativeInfo.StreamInfo OpenAndReport(StreamParameters? input, StreamParameters? output, uint frames)
    {
        using PaStream stream = new(input, output, sampleRate, frames, StreamFlags.ClipOff, Silence, null);
        stream.Start();
        Thread.Sleep(200);
        NativeInfo.StreamInfo info = NativeInfo.Info(stream);
        stream.Stop();
        return info;
    }

    private static void RunStability(uint frames, int seconds, bool mute)
    {
        PassThrough passThrough = new(mute);
        using PaStream stream = new(
            InputParameters(), OutputParameters(), sampleRate, frames, StreamFlags.ClipOff, passThrough.Callback, null);
        stream.Start();
        Thread.Sleep(TimeSpan.FromSeconds(seconds));
        NativeInfo.StreamInfo info = NativeInfo.Info(stream);
        stream.Stop();
        Console.WriteLine(
            $"frames {frames}, {seconds} s{(mute ? ", output muted" : "")}: callbacks {passThrough.Callbacks}, "
            + $"input overflows {passThrough.InputOverflows}, output underflows {passThrough.OutputUnderflows}, "
            + $"peak input {passThrough.PeakInput:F4}, reported latency in {Ms(info.InputLatency)} + out {Ms(info.OutputLatency)} ms");
    }

    private static void RunSeparateStability(uint frames, int seconds, bool mute)
    {
        SeparatePassThrough passThrough = new(frames, sampleRate, mute);
        using PaStream input = new(
            InputParameters(), null, sampleRate, frames, StreamFlags.ClipOff, passThrough.InputCallback, null);
        using PaStream output = new(
            null, OutputParameters(), sampleRate, frames, StreamFlags.ClipOff, passThrough.OutputCallback, null);
        input.Start();
        output.Start();
        Thread.Sleep(TimeSpan.FromSeconds(seconds));
        NativeInfo.StreamInfo inputInfo = NativeInfo.Info(input);
        NativeInfo.StreamInfo outputInfo = NativeInfo.Info(output);
        output.Stop();
        input.Stop();
        Console.WriteLine(
            $"separate, frames {frames}, {seconds} s{(mute ? ", output muted" : "")}: callbacks in {passThrough.InputCallbacks} / out {passThrough.OutputCallbacks}, "
            + $"input overflows {passThrough.InputOverflows}, output underflows {passThrough.OutputUnderflows}, "
            + $"ring overruns {passThrough.RingOverruns}, ring underruns {passThrough.RingUnderruns}, drift skips {passThrough.Skips} ({passThrough.SkippedFrames} frames), "
            + $"average ring fill {Ms(passThrough.AverageFill / sampleRate)} ms, "
            + $"reported latency in {Ms(inputInfo.InputLatency)} + out {Ms(outputInfo.OutputLatency)} ms");
    }

    private static StreamParameters InputParameters()
    {
        int device = PortAudio.DefaultInputDevice;
        return Parameters(device, 1, PortAudio.GetDeviceInfo(device).defaultLowInputLatency);
    }

    private static StreamParameters OutputParameters()
    {
        int device = PortAudio.DefaultOutputDevice;
        return Parameters(device, OutputChannels, PortAudio.GetDeviceInfo(device).defaultLowOutputLatency);
    }

    private static StreamParameters Parameters(int device, int channels, double suggestedLatency)
    {
        return new StreamParameters
        {
            device = device,
            channelCount = channels,
            sampleFormat = SampleFormat.Float32,
            suggestedLatency = suggestedLatency,
            hostApiSpecificStreamInfo = IntPtr.Zero,
        };
    }

    private static unsafe StreamCallbackResult Silence(
        IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        if (output != IntPtr.Zero)
        {
            new Span<float>((void*)output, (int)frameCount * OutputChannels).Clear();
        }

        return StreamCallbackResult.Continue;
    }

    private static string? OptionValue(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static string Ms(double seconds) => $"{seconds * 1000:F1}";
}
