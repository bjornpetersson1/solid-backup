namespace SolidBackup.Measurement;

/// <summary>A detector the corpus scorer can measure: one report per buffer, in order, from the start of a clip.</summary>
public interface ICorpusDetector
{
    DetectorReport Process(ReadOnlySpan<float> buffer);
}
