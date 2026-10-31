using SolidBackup.Audio;
using SolidBackup.Configuration;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// Plays the input straight to the output until q, Esc or Ctrl+C, so the audio path can be
/// checked by ear. Every way of stopping closes the audio and prints the dropout counts.
/// </summary>
public sealed class PassThroughCommand(
    IAnsiConsole console, Func<SettingsLoadResult> loadSettings, Func<AppSettings, IAudioDevices> openDevices)
{
    public const string RunningMessage = "Medhörning pågår: mikrofonen spelas upp i utgången. Tryck q eller Esc för att sluta.";
    public const string NotInteractiveMessage = "Medhörningen kräver en interaktiv terminal.";

    private static readonly TimeSpan KeyPollInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>Runs until a stop key, Ctrl+C or <paramref name="cancellation"/>.</summary>
    public int Run(CancellationToken cancellation = default)
    {
        if (!console.Profile.Capabilities.Interactive)
        {
            console.MarkupLine($"[red]{NotInteractiveMessage}[/]");
            return 1;
        }

        SettingsLoadResult loaded = loadSettings();
        if (loaded.Message is { } message)
        {
            console.MarkupLineInterpolated($"[yellow]{message}[/]");
        }

        try
        {
            using IAudioDevices devices = openDevices(loaded.Settings);
            RunUntilStopped(devices, cancellation);
            console.WriteLine(Summary(devices.Counters));
            return 0;
        }
        catch (AudioDeviceException exception)
        {
            console.MarkupLineInterpolated($"[red]Ljudet kunde inte startas: {exception.Message}[/]");
            return 1;
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
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

        // Ctrl+C would end the process at once; once the audio runs, turn it into a normal stop so
        // the audio closes. Before that, opening can block on the permission dialog, and Ctrl+C
        // must still be able to end the process.
        ConsoleCancelEventHandler stopOnCtrlC = (_, args) =>
        {
            args.Cancel = true;
            stop.Cancel();
        };
        pipeline.Start();
        Console.CancelKeyPress += stopOnCtrlC;
        try
        {
            console.WriteLine(RunningMessage);
            WaitForStop(stop.Token);
            pipeline.Stop();
        }
        finally
        {
            Console.CancelKeyPress -= stopOnCtrlC;
        }
    }

    // Polls instead of blocking in ReadKey, so a cancellation is noticed without a key press.
    private void WaitForStop(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            if (!console.Input.IsKeyAvailable())
            {
                stop.WaitHandle.WaitOne(KeyPollInterval);
                continue;
            }

            if (console.Input.ReadKey(intercept: true) is { } key && IsStopKey(key))
            {
                return;
            }
        }
    }

    private static bool IsStopKey(ConsoleKeyInfo key)
    {
        return key.Key == ConsoleKey.Escape || char.ToLowerInvariant(key.KeyChar) == 'q';
    }
}
