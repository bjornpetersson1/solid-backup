using System.Diagnostics.CodeAnalysis;

using SolidBackup.Accompaniment;
using SolidBackup.Configuration;

namespace SolidBackup.Cli;

/// <summary>
/// The one-run options that change what the chain plays (<c>--style</c>, <c>--key</c>, <c>--mode</c>, <c>--sound</c>,
/// <c>--features</c> and the rhythm), shared by <c>--accompany</c> and <c>--live</c>; null keeps the setting. They are
/// never saved.
/// </summary>
public sealed record SettingOverrides(
    string? Style = null, string? Key = null, string? Mode = null, string? Sound = null, string? Features = null, RhythmOverrides? Rhythm = null)
{
    /// <summary>The settings with the options applied, or false and the Swedish message when <c>--features</c> names a feature that does not exist.</summary>
    public bool TryApply(AppSettings settings, out AppSettings applied, [NotNullWhen(false)] out string? error)
    {
        applied = (Rhythm?.Apply(settings) ?? settings) with
        {
            HarmonyStyle = Style ?? settings.HarmonyStyle,
            Key = Key ?? settings.Key,
            Mode = Mode ?? settings.Mode,
            Sound = Sound ?? settings.Sound,
        };
        error = null;
        if (Features is not { } features)
        {
            return true;
        }

        string[] names = features == AccompanyCommand.NoFeatures ? [] : features.Split(',', StringSplitOptions.TrimEntries);
        if (LevelFeatures.TrySwitchOnly(applied, names, LevelFeatures.All, out applied))
        {
            return true;
        }

        error = AccompanyCommand.UnknownFeatureMessage(features);
        return false;
    }
}
