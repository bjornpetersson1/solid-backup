using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Configuration;
using SolidBackup.Recording;

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
    AudioPreflight preflight,
    Func<AppSettings, (string Path, Stream Stream)>? createRecording = null)
{
    public const string RunningMessage = "Medhörning pågår: mikrofonen spelas upp i utgången. Tryck q eller Esc för att sluta.";
    public const string NotInteractiveMessage = "Medhörningen kräver en interaktiv terminal.";

    private readonly AudioSession _session = new(console, openDevices, preflight, createRecording);

    /// <summary>Runs until a stop key, Ctrl+C or <paramref name="cancellation"/>; with <paramref name="record"/>, records the input and the output to WAV.</summary>
    public int Run(CancellationToken cancellation = default, bool record = false)
    {
        if (!console.Profile.Capabilities.Interactive)
        {
            return ConsoleMessages.Error(console, NotInteractiveMessage);
        }

        SettingsLoadResult loaded = loadSettings();
        ConsoleMessages.SettingsMessage(console, loaded);
        return _session.Run(loaded.Settings, record, () => new PassThrough(), cancellation);
    }

    public static string Summary(AudioCounters counters)
    {
        return $"Medhörningen stoppad. Överflöden i ingången: {counters.InputOverflows}, "
            + $"underflöden i utgången: {counters.OutputUnderflows}, "
            + $"tomma överlämningar: {counters.HandOverUnderruns}, "
            + $"tappade överlämningar: {counters.HandOverOverruns}, "
            + $"driftkorrigeringar: {counters.DriftCorrections}.";
    }

    private sealed class PassThrough : IAudioActivity
    {
        private AudioPipeline? _pipeline;
        private IAudioDevices? _devices;

        public void Start(AudioStart start)
        {
            _devices = start.Devices;
            _pipeline = new AudioPipeline(start.Devices.Input, start.Devices.Output, new PassThroughProcessor(), start.Recorder);
            _pipeline.Start();
        }

        public void WaitForStop(IAnsiConsole console, CancellationToken cancellation)
        {
            console.WriteLine(RunningMessage);
            StopKeys.WaitForStop(console, () => false, cancellation);
        }

        public void Stop()
        {
            _pipeline?.Stop();
        }

        public string Summary()
        {
            return PassThroughCommand.Summary(_devices?.Counters ?? default);
        }
    }
}
