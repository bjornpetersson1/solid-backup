namespace SolidBackup.Audio;

/// <summary>Dropout counts for one run; the basis for the dropout counter in live mode.</summary>
public readonly record struct AudioCounters(
    long InputOverflows,
    long OutputUnderflows,
    long HandOverUnderruns,
    long HandOverOverruns,
    long DriftCorrections);
