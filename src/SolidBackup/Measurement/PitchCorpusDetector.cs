using System.Diagnostics;

using SolidBackup.Analysis;

namespace SolidBackup.Measurement;

/// <summary>Measures a <see cref="PitchDetector"/> on the corpus, and the time it spends per buffer.</summary>
public sealed class PitchCorpusDetector(PitchDetector detector) : ICorpusDetector
{
    public long Buffers { get; private set; }

    public TimeSpan AnalysisTime { get; private set; }

    public DetectorReport Process(ReadOnlySpan<float> buffer)
    {
        long start = Stopwatch.GetTimestamp();
        PitchEstimate estimate = detector.Process(buffer);
        AnalysisTime += Stopwatch.GetElapsedTime(start);
        Buffers++;
        return new DetectorReport(estimate.TimeMs, estimate.HasNote ? estimate.MidiCents : null);
    }
}
