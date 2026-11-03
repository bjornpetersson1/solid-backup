using SolidBackup.Accompaniment;
using SolidBackup.Audio;
using SolidBackup.Recording;

namespace SolidBackup.Live;

/// <summary>
/// The accompaniment split over the two audio streams. On each input buffer (<see cref="OnBuffer"/>, the input's
/// thread) it analyses and decides what to play; on each output buffer (<see cref="Render"/>, the output's thread) it
/// plays the latest decision through the rhythm pattern and the synthesizer. Only decisions cross over, through a
/// <see cref="SnapshotSlot{T}"/>, so input jitter and clock drift can delay a chord by a buffer but never cut the
/// sound. A strike is counted, not just flagged, so one decided between two output buffers is still played. The
/// pattern's grid counts the output's samples. A recording of the output starts with the first output buffer after
/// the input's first, so the two channels start together, and holds the chords only. With an <see cref="InputMonitor"/>
/// the input is mixed into the output after that. <see cref="FadeOut"/> lets the chord ring out before the audio stops
/// and stops the monitoring. Neither side allocates, locks or does I/O.
/// </summary>
public sealed class LiveChain : IAudioInputHandler, IAudioRenderer
{
    private readonly Accompanist _accompanist;
    private readonly SessionRecorder? _recorder;
    private readonly InputMonitor? _monitor;
    private readonly SnapshotSlot<Decision> _decisions = new();
    private readonly SnapshotSlot<LiveState> _state = new();

    // The input's side.
    private long _strikes;

    // The output's side.
    private Decision _latest;
    private bool _inputStarted;
    private long _playedStrikes;
    private long _outputEnd;

    // Set from any thread; read by the output's.
    private volatile bool _fading;
    private volatile bool _monitorStopped;

    // The output's side: whether the monitor's fade has been played.
    private bool _monitorFaded;

    // Set by the output's thread after each buffer.
    private volatile bool _silent = true;

    /// <param name="recorder">Records the input from the input's thread and the output from the output's.</param>
    /// <param name="monitor">Mixes the input into the output; null for none.</param>
    public LiveChain(Accompanist accompanist, SessionRecorder? recorder = null, InputMonitor? monitor = null)
    {
        _accompanist = accompanist;
        _recorder = recorder;
        _monitor = monitor;
    }

    public void OnBuffer(ReadOnlySpan<float> samples)
    {
        AnalysisFrame analysis = _accompanist.Analyze(samples);
        AccompanimentFrame frame = _accompanist.Decide(analysis);
        if (frame.Strike)
        {
            _strikes++;
        }

        _decisions.Publish(new Decision(frame, _strikes));
        _state.Publish(new LiveState(
            analysis.Estimate, analysis.LevelDbfs, analysis.GateOpen, frame.Sounding ? frame.Chord : null, _accompanist.Harmonizer.CurrentKey));
        _recorder?.OnInput(samples);
        _monitor?.Write(samples);
    }

    public void Render(Span<float> output)
    {
        if (_decisions.TryRead(out Decision latest))
        {
            _latest = latest;
            _inputStarted = true;
        }

        AccompanimentFrame frame = _fading
            ? AccompanimentFrame.Neutral(null, sounding: false)
            : _latest.Frame with { Strike = _latest.Strikes != _playedStrikes };
        _playedStrikes = _latest.Strikes;
        _outputEnd += output.Length;
        _accompanist.Perform(AnalysisFrame.Position(_outputEnd, output.Length, _accompanist.SampleRate), frame, output);
        _silent = _accompanist.Synthesizer.IsSilent;
        if (_inputStarted)
        {
            _recorder?.OnOutput(output);
        }

        if (_monitor is not null && !_monitorStopped && !_monitorFaded)
        {
            _monitorFaded = _fading;
            _monitor.MixInto(output, fadeOut: _fading);
        }
    }

    /// <summary>Whether the input is mixed into the output: there is a monitor and it has not been stopped.</summary>
    public bool Monitoring => _monitor is not null && !_monitorStopped;

    /// <summary>From the next output buffer on the input is not mixed in, for good (the output became unsafe).</summary>
    public void StopMonitoring()
    {
        _monitorStopped = true;
    }

    /// <summary>True when the last output buffer ended in silence (or none has been played).</summary>
    public bool IsSilent => _silent;

    /// <summary>From here on nothing new sounds: the chord is released and rings out over the sound's release.</summary>
    public void FadeOut()
    {
        _fading = true;
    }

    /// <summary>The state after the latest input buffer, for a reader on another thread; false while a write overlaps or before the first.</summary>
    public bool TryReadState(out LiveState state)
    {
        return _state.TryRead(out state);
    }

    private readonly record struct Decision(AccompanimentFrame Frame, long Strikes);
}
