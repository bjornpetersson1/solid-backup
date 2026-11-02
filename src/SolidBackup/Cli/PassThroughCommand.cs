using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Configuration;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// Plays the input straight to the output until q, Esc or Ctrl+C, so the audio path can be
/// checked by ear. It stops before opening anything when the microphone is blocked, and warns
/// when the output is the built-in speakers. Every way of stopping closes the audio and prints
/// the dropout counts.
/// </summary>
public sealed class PassThroughCommand(
    IAnsiConsole console,
    Func<SettingsLoadResult> loadSettings,
    Func<AppSettings, IAudioDevices> openDevices,
    AudioPreflight preflight)
{
    public const string RunningMessage = "Medhörning pågår: mikrofonen spelas upp i utgången. Tryck q eller Esc för att sluta.";
    public const string NotInteractiveMessage = "Medhörningen kräver en interaktiv terminal.";

    /// <summary>Runs until a stop key, Ctrl+C or <paramref name="cancellation"/>.</summary>
    public int Run(CancellationToken cancellation = default)
    {
        if (!console.Profile.Capabilities.Interactive)
        {
            return ConsoleMessages.Error(console, NotInteractiveMessage);
        }

        SettingsLoadResult loaded = loadSettings();
        ConsoleMessages.SettingsMessage(console, loaded);

        MicrophoneCheck microphone = preflight.CheckMicrophone();
        if (microphone is { Blocked: true, Message: { } problem })
        {
            return ConsoleMessages.Error(console, problem);
        }

        if (microphone.Message is { } notice)
        {
            ConsoleMessages.Warning(console, notice);
        }

        try
        {
            using IAudioDevices devices = openDevices(loaded.Settings);
            if (preflight.SpeakerProblem(devices.OutputDeviceName) is { } warning)
            {
                ConsoleMessages.Warning(console, warning);
            }

            RunUntilStopped(devices, cancellation);
            console.WriteLine(Summary(devices.Counters));
            return 0;
        }
        catch (AudioDeviceException exception)
        {
            return ConsoleMessages.AudioError(console, exception);
        }
    }

    public static string Summary(AudioCounters counters)
    {
        return $"Medhörningen stoppad. Överflöden i ingången: {counters.InputOverflows}, "
            + $"underflöden i utgången: {counters.OutputUnderflows}, "
            + $"tomma överlämningar: {counters.HandOverUnderruns}, "
            + $"tappade överlämningar: {counters.HandOverOverruns}, "
            + $"driftkorrigeringar: {counters.DriftCorrections}.";
    }

    private void RunUntilStopped(IAudioDevices devices, CancellationToken cancellation)
    {
        AudioPipeline pipeline = new(devices.Input, devices.Output, new PassThroughProcessor());
        pipeline.Start();
        console.WriteLine(RunningMessage);
        StopKeys.WaitForStop(console, () => false, cancellation);
        pipeline.Stop();
    }
}
