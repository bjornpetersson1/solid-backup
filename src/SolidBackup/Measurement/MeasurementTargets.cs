namespace SolidBackup.Measurement;

/// <summary>The targets and tolerances from Measurement in <c>Constitution/Requirements-and-goal.md</c>.</summary>
public static class MeasurementTargets
{
    public const double MinPitchAccuracy = 0.95;
    public const double MaxOctaveErrors = 0.01;
    public const double MaxFalseNotes = 0.02;
    public const double MinOnsetAccuracy = 0.90;

    /// <summary>Silence and breath give no note at all with the gate on (E3-3).</summary>
    public const int MaxNotesInSilenceOrBreath = 0;

    public const double PitchToleranceCents = 50;

    /// <summary>An octave or more off: 1200 cents less the ±50 cent tolerance.</summary>
    public const double OctaveErrorCents = 1150;

    public const double OnsetToleranceMs = 20;

    /// <summary>Windows this close to a segment boundary mix two states and are left out of the pitch measures.</summary>
    public const double BoundaryExclusionMs = 30;
}
