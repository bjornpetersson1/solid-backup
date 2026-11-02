using System.Diagnostics;
using System.Globalization;

using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Configuration;
using SolidBackup.Live;
using SolidBackup.Recording;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>How live mode ended: its exit code, and whether Ctrl+C (or a cancellation) stopped it.</summary>
public readonly record struct LiveExit(int ExitCode, bool Interrupted);

/// <summary>
/// Live mode: the microphone through the whole chain to the output, until q, Esc or Ctrl+C. It runs the same checks
/// and recording as <c>--passthrough</c> (<see cref="AudioSession"/>), shows one status line refreshed about twice a
/// second while it plays, lets the chord ring out on q or Esc (not on Ctrl+C), and ends with the dropouts and the
/// latency. <see cref="Play"/> is the entry point for settings that are already validated, such as the menu's.
/// </summary>
public sealed class LiveCommand(
    IAnsiConsole console,
    Func<SettingsLoadResult> loadSettings,
    Func<AppSettings, IAudioDevices> openDevices,
    AudioPreflight preflight,
    Func<AppSettings, (string Path, Stream Stream)>? createRecording = null)
{
    public const string RunningMessage = "Live-läget pågår. Tryck q eller Esc för att sluta.";
    public const string NotInteractiveMessage = "Live-läget kräver en interaktiv terminal.";

    private static readonly TimeSpan StatusInterval = TimeSpan.FromMilliseconds(500);

    private readonly AudioSession _session = new(console, openDevices, preflight, createRecording);

    /// <summary>
    /// Runs live mode with the saved settings and the one-run <paramref name="overrides"/>; it records when the saved
    /// <c>RecordOn</c> is on, or for this run with <paramref name="record"/>.
    /// </summary>
    public int Run(SettingOverrides overrides, bool record = false, CancellationToken cancellation = default)
    {
        if (!console.Profile.Capabilities.Interactive)
        {
            return ConsoleMessages.Error(console, NotInteractiveMessage);
        }

        SettingsLoadResult loaded = loadSettings();
        ConsoleMessages.SettingsMessage(console, loaded);
        if (!overrides.TryApply(loaded.Settings, out AppSettings settings, out string? error))
        {
            return ConsoleMessages.Error(console, error);
        }

        return HarmonySetup.Prepare(console, settings, overrides.Key is not null, overrides.Mode is not null) is null
            ? 1
            : Play(settings, record || settings.RecordOn, cancellation).ExitCode;
    }

    /// <summary>
    /// Runs live mode with validated settings until it is stopped. The exit code is 0 when it ran and a recording was
    /// written; <see cref="LiveExit.Interrupted"/> says it was stopped by Ctrl+C, so a caller such as the menu can quit.
    /// </summary>
    public LiveExit Play(AppSettings settings, bool record = false, CancellationToken cancellation = default)
    {
        LiveActivity? activity = null;

        // Measured before anything opens, so the devices and a recording are not held open while it runs.
        int exitCode = _session.Run(settings, record, () => activity = new LiveActivity(settings, ChainDelay.MeasureMs(settings)), cancellation);
        return new LiveExit(exitCode, activity?.Interrupted ?? false);
    }

    public static string StatusLine(LiveSnapshot snapshot, double targetMs)
    {
        return $"Ton {snapshot.State.Estimate} · Ackord {ChordText(snapshot.State)} · Tonart {KeyText(snapshot.State)} · "
            + $"Latens {LatencyText(snapshot.Latency, targetMs)} · Avbrott {snapshot.Counters.Dropouts}";
    }

    public static string Summary(LiveSnapshot snapshot, double targetMs)
    {
        AudioCounters counters = snapshot.Counters;
        return $"Live-läget stoppat. Avbrott: {counters.Dropouts} (överflöden i ingången: {counters.InputOverflows}, "
            + $"underflöden i utgången: {counters.OutputUnderflows}). Latens: {LatencyText(snapshot.Latency, targetMs)}, "
            + $"{PartsText(snapshot.Latency)}.";
    }

    /// <summary>The total and whether it meets the target, e.g. "38.2 ms (under målet 50 ms)"; "?" when the chain's delay is unknown.</summary>
    public static string LatencyText(LiveLatency latency, double targetMs)
    {
        string target = Ms(targetMs, "0.#");
        return latency.TotalMs is { } total
            ? $"{Ms(total)} ms ({(latency.Meets(targetMs) ? "under" : "över")} målet {target} ms)"
            : $"? ms (målet {target} ms)";
    }

    public static string PartsText(LiveLatency latency)
    {
        string chain = latency.ChainMs is { } ms ? Ms(ms) : "?";
        return $"ingång {Ms(latency.InputMs)} + kedja {chain} + överlämning {Ms(latency.HandOverMs)} + utgång {Ms(latency.OutputMs)} ms";
    }

    private static string ChordText(LiveState state)
    {
        return state.Chord is { } chord ? chord.Symbol : "–";
    }

    private static string KeyText(LiveState state)
    {
        return state.Key is { } key ? HarmonyNames.Key(key) : "–";
    }

    private static string Ms(double ms, string format = "0.0")
    {
        return ms.ToString(format, CultureInfo.InvariantCulture);
    }

    private sealed class LiveActivity(AppSettings settings, double? chainDelayMs) : IAudioActivity
    {
        private LiveMode? _mode;

        public bool Interrupted { get; private set; }

        public void Start(IAudioDevices devices, SessionRecorder? recorder)
        {
            _mode = new LiveMode(devices, settings, chainDelayMs, recorder);
            _mode.Start();
        }

        public void WaitForStop(IAnsiConsole console, CancellationToken cancellation)
        {
            console.WriteLine(RunningMessage);
            Stopwatch sinceStatus = Stopwatch.StartNew();
            ShowStatus(console);
            StopReason reason = StopKeys.WaitForStop(
                console,
                () =>
                {
                    if (sinceStatus.Elapsed >= StatusInterval)
                    {
                        ShowStatus(console);
                        sinceStatus.Restart();
                    }

                    return false;
                },
                cancellation);
            console.WriteLine();

            // q and Esc let the chord ring out; Ctrl+C stops at once, also during the fade.
            if (reason == StopReason.StopKey)
            {
                _mode?.FadeOut(cancellation);
            }

            Interrupted = reason == StopReason.Interrupted || cancellation.IsCancellationRequested;
        }

        public void Stop()
        {
            _mode?.Stop();
        }

        public string Summary()
        {
            return LiveCommand.Summary(Snapshot(), settings.LatencyTargetMs);
        }

        // Rewrites the one line in place: back to its start, the status, and the rest of the old line cleared. It is
        // written as ANSI, since Spectre would wrap or escape the control characters in plain text, and cut to the
        // terminal's width, since a line that wraps could not be rewritten.
        private void ShowStatus(IAnsiConsole console)
        {
            string line = StatusLine(Snapshot(), settings.LatencyTargetMs);
            int width = Math.Max(1, console.Profile.Width - 1);
            console.WriteAnsi($"\r{(line.Length > width ? line[..width] : line)}\u001b[K");
        }

        private LiveSnapshot Snapshot()
        {
            return (_mode ?? throw new InvalidOperationException("Live mode has not started.")).Snapshot();
        }
    }
}
