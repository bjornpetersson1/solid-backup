using SolidBackup.Measurement;

namespace SolidBackup.Tests.Measurement;

/// <summary>A detector that echoes the reference: the pitch at each buffer's centre and every onset in the buffer.</summary>
internal sealed class OracleDetector(ClipReference reference, int sampleRate) : ICorpusDetector
{
    private long _frames;

    public DetectorReport Process(ReadOnlySpan<float> buffer)
    {
        double startMs = _frames * 1000.0 / sampleRate;
        _frames += buffer.Length;
        double endMs = _frames * 1000.0 / sampleRate;
        double centreMs = (startMs + endMs) / 2;
        double? pitch = reference.SegmentAt(centreMs) is { HasPitch: true } segment
            ? segment.PitchAt(centreMs)
            : null;
        double? onset = reference.OnsetsMs.Where(time => time >= startMs && time < endMs).Select(time => (double?)time).FirstOrDefault();
        return new DetectorReport(centreMs, pitch, onset);
    }
}
