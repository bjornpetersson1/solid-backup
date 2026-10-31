using System.Diagnostics;

using SolidBackup.Analysis;
using SolidBackup.Configuration;

namespace SolidBackup.Measurement;

/// <summary>
/// Measures the analysis components together on the corpus: the pitch detector, the level meter with the
/// noise gate, and the onset detector, each made from the settings; and the time they spend per buffer.
/// The pitch estimate refers to a moment further back than the gate's level, so each estimate is paired
/// with the gate's state at that moment.
/// </summary>
public sealed class AnalysisCorpusDetector : ICorpusDetector
{
    // Gate states by the time their level refers to, enough to reach back over the pitch detector's delay.
    private const int GateHistory = 256;

    private readonly double[] _gateTimes = new double[GateHistory];
    private readonly bool[] _gateStates = new bool[GateHistory];
    private readonly int _sampleRate;
    private int _gateCount;
    private long _position;

    private readonly PitchDetector _pitch;
    private readonly LevelMeter _level;
    private readonly NoiseGate _gate;
    private readonly OnsetDetector _onsets;

    public AnalysisCorpusDetector(AppSettings settings)
    {
        _sampleRate = settings.SampleRate;
        _pitch = PitchDetector.FromSettings(settings);
        _level = new LevelMeter(settings.SampleRate);
        _gate = new NoiseGate(settings.SampleRate, settings.GateThresholdDbfs, settings.GateHysteresisDb, settings.GateHoldMs);
        _onsets = new OnsetDetector(settings.SampleRate, settings.OnsetThresholdDb, settings.OnsetMinIntervalMs, _level.WindowLength);
    }

    public long Buffers { get; private set; }

    public TimeSpan AnalysisTime { get; private set; }

    public DetectorReport Process(ReadOnlySpan<float> buffer)
    {
        long start = Stopwatch.GetTimestamp();
        PitchEstimate estimate = _pitch.Process(buffer);
        double level = _level.Process(buffer);
        bool open = _gate.Process(level, buffer.Length);
        double? onset = _onsets.Process(level, buffer.Length);
        AnalysisTime += Stopwatch.GetElapsedTime(start);
        Buffers++;
        _position += buffer.Length;
        RememberGate((_position - (_level.WindowLength / 2.0)) * 1000 / _sampleRate, open);
        return new DetectorReport(estimate.TimeMs, estimate.HasNote ? estimate.MidiCents : null, onset, GateAt(estimate.TimeMs));
    }

    private void RememberGate(double timeMs, bool open)
    {
        int index = _gateCount % GateHistory;
        _gateTimes[index] = timeMs;
        _gateStates[index] = open;
        _gateCount++;
    }

    // The gate's state from the latest level at or before the time; before any, the earliest known.
    private bool GateAt(double timeMs)
    {
        int oldest = Math.Max(0, _gateCount - GateHistory);
        for (int i = _gateCount - 1; i > oldest; i--)
        {
            if (_gateTimes[i % GateHistory] <= timeMs)
            {
                return _gateStates[i % GateHistory];
            }
        }

        return _gateStates[oldest % GateHistory];
    }
}
