using System.Diagnostics;

using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Configuration;
using SolidBackup.Live;
using SolidBackup.Recording;

using Spectre.Console;
using Spectre.Console.Rendering;

namespace SolidBackup.Cli;

/// <summary>How live mode ended: its exit code, and whether Ctrl+C (or a cancellation) stopped it.</summary>
public readonly record struct LiveExit(int ExitCode, bool Interrupted);

/// <summary>
/// Live mode: the microphone through the whole chain to the output, until q, Esc or Ctrl+C. It runs the same checks
/// and recording as <c>--passthrough</c> (<see cref="AudioSession"/>), shows the dashboard (<see cref="LiveDashboard"/>)
/// redrawn about five times a second while it plays, lets the chord ring out on q or Esc (not on Ctrl+C), and ends with the dropouts and the
/// latency. <see cref="Play"/> is the entry point for settings that are already validated, such as the menu's.
/// </summary>
public sealed class LiveCommand(
    IAnsiConsole console,
    Func<SettingsLoadResult> loadSettings,
    Func<AppSettings, IAudioDevices> openDevices,
    AudioPreflight preflight,
    Func<AppSettings, (string Path, Stream Stream)>? createRecording = null,
    bool returnsToMenu = false)
{
    public const string NotInteractiveMessage = "Live-läget kräver en interaktiv terminal.";

    /// <summary>How often live mode checks again that the output is safe for monitoring, while it monitors.</summary>
    public static readonly TimeSpan MonitorCheckInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>How often the dashboard is redrawn: a few times a second.</summary>
    public static readonly TimeSpan RedrawInterval = TimeSpan.FromMilliseconds(200);

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
        int exitCode = _session.Run(
            settings, record, () => activity = new LiveActivity(settings, ChainDelay.MeasureMs(settings), returnsToMenu), cancellation);
        return new LiveExit(exitCode, activity?.Interrupted ?? false);
    }

    public static string Summary(LiveSnapshot snapshot)
    {
        AudioCounters counters = snapshot.Counters;
        LiveLatency latency = snapshot.Latency;
        int target = snapshot.Settings.LatencyTargetMs;
        string meets = latency.TotalMs is null ? $"målet {target} ms" : $"{(latency.Meets(target) ? "under" : "över")} målet {target} ms";
        return $"Live-läget stoppat. Avbrott: {counters.Dropouts} (överflöden i ingången: {counters.InputOverflows}, "
            + $"underflöden i utgången: {counters.OutputUnderflows}). Latens: {LatencyTexts.Total(latency)} ms ({meets}), "
            + $"ingång {LatencyTexts.Ms(latency.InputMs)} + kedja {LatencyTexts.Chain(latency)} + överlämning {LatencyTexts.Ms(latency.HandOverMs)} "
            + $"+ utgång {LatencyTexts.Ms(latency.OutputMs)} ms.";
    }

    private sealed class LiveActivity(AppSettings settings, double? chainDelayMs, bool returnsToMenu) : IAudioActivity
    {
        private LiveMode? _mode;
        private bool _onSpeakers;
        private Func<bool> _outputSafe = () => false;

        public bool Interrupted { get; private set; }

        public void Start(AudioStart start)
        {
            _onSpeakers = start.SpeakerWarning is not null;
            _outputSafe = start.OutputSafeForMonitoring;
            _mode = new LiveMode(start.Devices, settings, chainDelayMs, start.Recorder, _outputSafe());
            _mode.Start();
        }

        // The dashboard runs on this thread and only reads snapshots, so it cannot hold up the audio; it ends before
        // the fade and the stop.
        public void WaitForStop(IAnsiConsole console, CancellationToken cancellation)
        {
            if (settings.MonitorOn && _mode is { Monitoring: false })
            {
                ConsoleMessages.Warning(console, SettingTexts.MonitorOffUnsafeOutput);
            }

            Stopwatch clock = Stopwatch.StartNew();
            RedrawPacer pacer = new(RedrawInterval, () => clock.Elapsed);
            MonitorGuard guard = new(_mode, _outputSafe, new RedrawPacer(MonitorCheckInterval, () => clock.Elapsed));
            StopReason reason = StopReason.Interrupted;
            console.Live(Dashboard(console)).Start(live =>
            {
                reason = StopKeys.WaitForStop(
                    console,
                    () =>
                    {
                        guard.Check();
                        if (pacer.Due())
                        {
                            live.UpdateTarget(Dashboard(console));
                        }

                        return false;
                    },
                    cancellation);
            });

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
            return LiveCommand.Summary(Snapshot());
        }

        private IRenderable Dashboard(IAnsiConsole console)
        {
            return LiveDashboard.Render(Snapshot(), new DashboardContext(returnsToMenu, _onSpeakers, _mode?.Monitoring ?? false), console.Profile.Width);
        }

        private LiveSnapshot Snapshot()
        {
            return (_mode ?? throw new InvalidOperationException("Live mode has not started.")).Snapshot();
        }
    }
}
