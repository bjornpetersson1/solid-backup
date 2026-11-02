namespace SolidBackup.Audio;

/// <summary>Dropout counts for one run; the basis for the dropout counter in live mode.</summary>
public readonly record struct AudioCounters(
    long InputOverflows,
    long OutputUnderflows,
    long HandOverUnderruns,
    long HandOverOverruns,
    long DriftCorrections)
{
    /// <summary>The dropouts as Measurement counts them: the overflows and underflows the audio system reports.</summary>
    public long Dropouts => InputOverflows + OutputUnderflows;
}
