using SolidBackup.Configuration;

namespace SolidBackup.Cli;

/// <summary><c>--pattern</c>, <c>--bpm</c> and <c>--time-signature</c> for one run; null keeps the setting.</summary>
public sealed record RhythmOverrides(string? Pattern = null, int? Bpm = null, string? TimeSignature = null)
{
    /// <summary>The settings with <paramref name="overrides"/> applied; none leaves them as they are.</summary>
    public static AppSettings Apply(RhythmOverrides? overrides, AppSettings settings)
    {
        return overrides?.Apply(settings) ?? settings;
    }

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
