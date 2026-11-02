using SolidBackup.Analysis;
using SolidBackup.Audio;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Synthesis;

namespace SolidBackup.Accompaniment;

/// <summary>
/// The whole chain for one buffer: analysis (pitch, level, gate, onsets), the level features that are on, the
/// harmony, the rhythm pattern (<see cref="RhythmPatterns"/>) and the synthesizer. Without the gate, the chord keeps sounding through
/// silence until <c>PhraseResetMs</c> has passed (<c>docs/rhythm.md</c>). Everything is built in the constructor,
/// so a buffer allocates nothing; the chain is deterministic, so a recording always gives the same output.
/// Live, <see cref="Analyze"/> and <see cref="Decide"/> run on the input's thread and <see cref="Perform"/> on the
/// output's: the two halves share no state.
/// </summary>
public sealed class Accompanist : IAudioProcessor
{
    private readonly PitchDetector _pitch;
    private readonly LevelMeter _level;
    private readonly NoiseGate _gate;
    private readonly OnsetDetector _onsets;
    private readonly ILevelFeature[] _features;
    private readonly IRhythmPattern _pattern;
    private readonly LevelSmoother _smoother;
    private readonly int _sampleRate;
    private readonly long _followSamples;
    private long _position;
    private long _gateOpenedAt = long.MinValue;
    private bool _gateWasOpen;

    /// <summary>Builds the chain from validated settings, with the features of <paramref name="registry"/> (default <see cref="LevelFeatures.All"/>) that are on.</summary>
    public Accompanist(AppSettings settings, IReadOnlyList<LevelFeatureEntry>? registry = null)
    {
        _sampleRate = settings.SampleRate;
        _pitch = PitchDetector.FromSettings(settings);
        _level = new LevelMeter(settings.SampleRate);
        _gate = new NoiseGate(settings.SampleRate, settings.GateThresholdDbfs, settings.GateHysteresisDb, settings.GateHoldMs);
        _smoother = new LevelSmoother(settings.SampleRate);
        _followSamples = 2L * _level.WindowLength;
        _onsets = new OnsetDetector(settings.SampleRate, settings.OnsetThresholdDb, settings.OnsetMinIntervalMs, _level.WindowLength);
        _features = [.. (registry ?? LevelFeatures.All).Where(entry => entry.IsOn(settings)).Select(entry => entry.Create(settings))];
        Harmonizer = Harmonizer.FromSettings(settings);
        _pattern = RhythmPatterns.Create(settings);
        Synthesizer = Synthesizer.FromSettings(settings);
    }

    public int SampleRate => _sampleRate;

    public Harmonizer Harmonizer { get; }

    public Synthesizer Synthesizer { get; }

    /// <summary>What the features asked the synthesizer to play for the last buffer.</summary>
    public AccompanimentFrame Frame { get; private set; }

    public void Process(ReadOnlySpan<float> input, Span<float> output)
    {
        Play(Analyze(input), output);
    }

    /// <summary>The analysis of the next buffer: pitch, level, gate and onset.</summary>
    public AnalysisFrame Analyze(ReadOnlySpan<float> input)
    {
        PitchEstimate estimate = _pitch.Process(input);
        double level = _level.Process(input);
        bool open = _gate.Process(level, input.Length);
        bool onset = _onsets.Process(level, input.Length) is not null;
        _position += input.Length;
        if (open && !_gateWasOpen)
        {
            _gateOpenedAt = _position;
        }

        _gateWasOpen = open;

        // The smoothing would start each phrase from the silence floor, so right after the gate opens (while the level
        // meter's window fills) the smoothed level follows the level; within a phrase it is smoothed.
        double smoothed = _position - _gateOpenedAt <= _followSamples ? _smoother.Follow(level) : _smoother.Process(level, input.Length);
        return new AnalysisFrame(_position, AnalysisFrame.TimeMsAt(_position, _sampleRate), input.Length, level, smoothed, open, onset, estimate);
    }

    /// <summary>
    /// Plays one analysed buffer: the features, the harmony and the synthesizer, rendered into <paramref name="output"/>.
    /// Analyses can come from another accompanist with the same analysis settings, so one analysis serves several runs.
    /// </summary>
    public void Play(in AnalysisFrame analysis, Span<float> output)
    {
        AccompanimentFrame frame = Decide(analysis);
        Perform(analysis, frame, output);
    }

    /// <summary>What to play for one analysed buffer: the features before the harmony, the harmony and the features after it.</summary>
    public AccompanimentFrame Decide(in AnalysisFrame analysis)
    {
        HarmonyInput harmonyInput = new(analysis.Estimate, Immediate: false);
        foreach (ILevelFeature feature in _features)
        {
            feature.BeforeHarmony(analysis, ref harmonyInput);
        }

        // A change on a beat is in effect for the buffer whose samples hold the beat, where the pattern plays it.
        Harmonizer.Process(harmonyInput.Estimate, harmonyInput.Immediate, AnalysisFrame.TimeMsAt(analysis.EndSample - 1, _sampleRate));
        AccompanimentFrame frame = AccompanimentFrame.Neutral(Harmonizer.Chord, Harmonizer.Chord is not null && Harmonizer.InPhrase) with
        {
            ChangeAwaitsBeat = Harmonizer.AwaitsBeat,
        };
        foreach (ILevelFeature feature in _features)
        {
            feature.AfterHarmony(analysis, ref frame);
        }

        Frame = frame;
        return frame;
    }

    /// <summary>
    /// Plays a decided frame through the rhythm pattern and renders the synthesizer into <paramref name="output"/>; the
    /// pattern takes its place on the grid from <paramref name="timing"/>.
    /// </summary>
    public void Perform(in AnalysisFrame timing, in AccompanimentFrame frame, Span<float> output)
    {
        _pattern.Play(timing, frame, Synthesizer);
        Synthesizer.Render(output);
    }
}
