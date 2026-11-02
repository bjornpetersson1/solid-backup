using System.Diagnostics;

using SolidBackup.Accompaniment;
using SolidBackup.Audio;
using SolidBackup.Configuration;
using SolidBackup.Recording;

namespace SolidBackup.Live;

/// <summary>
/// Live mode on opened devices: the <see cref="LiveChain"/> between the input and the output, its latency and the
/// snapshots for the dashboard.
/// </summary>
public sealed class LiveMode
{
    /// <summary>The longest <see cref="FadeOut"/> waits for the chord's release to ring out.</summary>
    public static readonly TimeSpan MaxFadeOut = TimeSpan.FromSeconds(1);

    // Often enough that the stop follows the silence closely, without keeping a core busy beside the audio threads.
    private static readonly TimeSpan FadePoll = TimeSpan.FromMilliseconds(10);

    private readonly IAudioDevices _devices;
    private readonly LiveChain _chain;
    private readonly double? _chainDelayMs;
    private readonly double _bufferMs;
    private readonly IReadOnlyList<string> _featuresOn;
    private readonly string _rhythmPattern;
    private LiveState _lastState;

    /// <param name="chainDelayMs">The chain's delay from <see cref="ChainDelay.MeasureMs"/>, measured before the devices open.</param>
    public LiveMode(IAudioDevices devices, AppSettings settings, double? chainDelayMs, SessionRecorder? recorder = null)
    {
        _devices = devices;
        _chain = new LiveChain(new Accompanist(settings), recorder);
        _chainDelayMs = chainDelayMs;
        _bufferMs = settings.BufferSize * 1000.0 / settings.SampleRate;
        _featuresOn = LevelFeatures.NamesOn(settings);
        _rhythmPattern = settings.RhythmPattern;
    }

    /// <summary>The latency parts; the device latencies are known once <see cref="Start"/> has opened the streams.</summary>
    public LiveLatency Latency
    {
        get
        {
            ReportedLatency reported = _devices.Latency;
            return new LiveLatency(reported.InputMs, _chainDelayMs, _bufferMs, reported.OutputMs);
        }
    }

    public void Start()
    {
        AudioStreams.Start(_devices.Input, _chain, _devices.Output, _chain);
    }

    /// <summary>
    /// Releases the chord and waits until it has rung out, at most <see cref="MaxFadeOut"/> or until
    /// <paramref name="cancellation"/>; call it before <see cref="Stop"/>.
    /// </summary>
    public void FadeOut(CancellationToken cancellation = default)
    {
        _chain.FadeOut();
        Stopwatch fading = Stopwatch.StartNew();
        while (!_chain.IsSilent && fading.Elapsed < MaxFadeOut && !cancellation.WaitHandle.WaitOne(FadePoll))
        {
        }
    }

    public void Stop()
    {
        AudioStreams.Stop(_devices.Input, _devices.Output);
    }

    /// <summary>The latest state, or the one before it while the input is writing; called off the audio threads.</summary>
    public LiveSnapshot Snapshot()
    {
        if (_chain.TryReadState(out LiveState state))
        {
            _lastState = state;
        }

        return new LiveSnapshot(_lastState, _featuresOn, _rhythmPattern, Latency, _devices.Counters);
    }
}
