using SolidBackup.Configuration;
using SolidBackup.Rhythm;

namespace SolidBackup.Accompaniment;

/// <summary>Builds the rhythm pattern the settings name, on a grid from their tempo and time signature.</summary>
public static class RhythmPatterns
{
    /// <exception cref="ArgumentException">The rhythm settings are not valid; validate them first.</exception>
    public static IRhythmPattern Create(AppSettings settings)
    {
        if (!SettingNames.TryParse(settings.RhythmPattern, out RhythmPatternName name))
        {
            throw new ArgumentException("The rhythm settings are not valid; validate them first.", nameof(settings));
        }

        TempoGrid grid = TempoGrid.FromSettings(settings);
        return name switch
        {
            RhythmPatternName.Arpeggio => new ArpeggioPattern(grid),
            RhythmPatternName.Strum => new StrumPattern(grid),
            _ => new HeldPattern(),
        };
    }
}
