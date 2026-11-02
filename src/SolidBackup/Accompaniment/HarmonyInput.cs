using SolidBackup.Analysis;

namespace SolidBackup.Accompaniment;

/// <summary>What reaches the harmony for one buffer: the estimate (a feature may hide its note) and whether a new note is evaluated at once.</summary>
public record struct HarmonyInput(PitchEstimate Estimate, bool Immediate);
