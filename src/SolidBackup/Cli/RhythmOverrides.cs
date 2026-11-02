using SolidBackup.Configuration;

namespace SolidBackup.Cli;

/// <summary><c>--pattern</c>, <c>--bpm</c> and <c>--time-signature</c> for one run; null keeps the setting.</summary>
public sealed record RhythmOverrides(string? Pattern = null, int? Bpm = null, string? TimeSignature = null)
{
    public AppSettings Apply(AppSettings settings)
    {
        return settings with
        {
            RhythmPattern = Pattern ?? settings.RhythmPattern,
            Bpm = Bpm ?? settings.Bpm,
            TimeSignature = TimeSignature ?? settings.TimeSignature,
        };
    }
}
