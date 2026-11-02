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
/// the input's first, so the two channels start together. Neither side allocates, locks or does I/O.
/// </summary>
public sealed class LiveChain : IAudioInputHandler, IAudioRenderer
{
    private readonly Accompanist _accompanist;
    private readonly SessionRecorder? _recorder;
    private readonly SnapshotSlot<Decision> _decisions = new();
    private readonly SnapshotSlot<LiveState> _state = new();

    // The input's side.
    private long _strikes;

    // The output's side.
    private Decision _latest;
    private bool _inputStarted;
    private long _playedStrikes;
    private long _outputEnd;

    /// <param name="recorder">Records the input from the input's thread and the output from the output's.</param>
    public LiveChain(Accompanist accompanist, SessionRecorder? recorder = null)
    {
        _accompanist = accompanist;
        _recorder = recorder;
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
    }

    public void Render(Span<float> output)
    {
        if (_decisions.TryRead(out Decision latest))
        {
            _latest = latest;
            _inputStarted = true;
        }

        AccompanimentFrame frame = _latest.Frame with { Strike = _latest.Strikes != _playedStrikes };
        _playedStrikes = _latest.Strikes;
        _outputEnd += output.Length;
        _accompanist.Perform(AnalysisFrame.Position(_outputEnd, output.Length, _accompanist.SampleRate), frame, output);
        if (_inputStarted)
        {
            _recorder?.OnOutput(output);
        }
    }

    /// <summary>The state after the latest input buffer, for a reader on another thread; false while a write overlaps or before the first.</summary>
    public bool TryReadState(out LiveState state)
    {
        return _state.TryRead(out state);
    }

    private readonly record struct Decision(AccompanimentFrame Frame, long Strikes);
}
