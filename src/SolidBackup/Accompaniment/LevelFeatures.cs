using SolidBackup.Configuration;

namespace SolidBackup.Accompaniment;

/// <summary>A level feature with its name (as <c>--features</c> takes it), its switch in the settings and how to build it.</summary>
public sealed record LevelFeatureEntry(
    string Name,
    Func<AppSettings, bool> IsOn,
    Func<AppSettings, bool, AppSettings> Switch,
    Func<AppSettings, ILevelFeature> Create);

/// <summary>
/// Every level feature, in the order they run: the gate decides whether anything sounds, onsets when the chord is
/// struck, chord character which tones, dynamics how strong. A new feature is a class, a setting and a line here.
/// </summary>
public static class LevelFeatures
{
    public static IReadOnlyList<LevelFeatureEntry> All { get; } =
    [
        new("gate", settings => settings.GateOn, (settings, on) => settings with { GateOn = on }, _ => new GateFeature()),
        new("onsets", settings => settings.OnsetsOn, (settings, on) => settings with { OnsetsOn = on }, _ => new OnsetFeature()),
        new(
            "character",
            settings => settings.ChordCharacterOn,
            (settings, on) => settings with { ChordCharacterOn = on },
            settings => new ChordCharacterFeature(settings.CharacterSparseBelowDbfs, settings.CharacterFullAboveDbfs)),
        new(
            "dynamics",
            settings => settings.DynamicsOn,
            (settings, on) => settings with { DynamicsOn = on },
            settings => new DynamicsFeature(settings.DynamicsQuietDbfs, settings.DynamicsLoudDbfs)),
    ];

    /// <summary>The names of the features that are on, in their order.</summary>
    public static IReadOnlyList<string> NamesOn(AppSettings settings)
    {
        return [.. All.Where(entry => entry.IsOn(settings)).Select(entry => entry.Name)];
    }

    /// <summary>The settings with exactly the named features on; false when a name is unknown.</summary>
    public static bool TrySwitchOnly(AppSettings settings, IReadOnlyCollection<string> names, IReadOnlyList<LevelFeatureEntry> registry, out AppSettings switched)
    {
        switched = settings;
        if (names.Any(name => registry.All(entry => entry.Name != name)))
        {
            return false;
        }

        foreach (LevelFeatureEntry entry in registry)
        {
            switched = entry.Switch(switched, names.Contains(entry.Name));
        }

        return true;
    }
}
