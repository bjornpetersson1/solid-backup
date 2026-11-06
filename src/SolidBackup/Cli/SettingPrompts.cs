using SolidBackup.Accompaniment;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Rhythm;
using SolidBackup.Synthesis;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// The menu's questions about a setting, each starting on the current value (E6-2), shared by the main menu and the
/// run menu of an imported clip (E8-4). Both menus offer the "music" settings; monitoring and recording are about live
/// mode, so only the main menu asks about them.
/// </summary>
public sealed class SettingPrompts(IAnsiConsole console)
{
    public const string KeyChoice = "Tonart";
    public const string StyleChoice = "Stil";
    public const string SoundChoice = "Ljud";
    public const string RhythmChoice = "Rytm";
    public const string ChordChangesChoice = "Ackordbyten";
    public const string FeaturesChoice = "Nivåfunktioner";
    public const string AutoplayChoice = "Autospel";

    /// <summary>The settings for how the chords are chosen and played, in the order the menus show them.</summary>
    public static IReadOnlyList<string> MusicChoices { get; } =
        [KeyChoice, StyleChoice, SoundChoice, RhythmChoice, ChordChangesChoice, FeaturesChoice, AutoplayChoice];

    public static string MonitorLevelMessage => $"Ange en nivå mellan {MonitorSettings.MinLevelPercent} och {MonitorSettings.MaxLevelPercent} %.";

    public static string BpmRangeMessage => $"Ange ett tempo mellan {RhythmSettings.MinBpm} och {RhythmSettings.MaxBpm} BPM.";

    /// <summary>One row per music setting, as the menus' tables show them.</summary>
    public static void AddMusicRows(Table table, AppSettings settings)
    {
        table.AddRow(KeyChoice, Markup.Escape(SettingTexts.Key(settings)));
        table.AddRow(StyleChoice, Markup.Escape(SettingTexts.Style(settings)));
        table.AddRow(SoundChoice, Markup.Escape(settings.Sound));
        table.AddRow(RhythmChoice, Markup.Escape(SettingTexts.Rhythm(settings)));
        table.AddRow(ChordChangesChoice, Markup.Escape(SettingTexts.ChordChanges(settings)));
        table.AddRow(FeaturesChoice, Markup.Escape(SettingTexts.Features(settings)));
        table.AddRow(AutoplayChoice, SettingTexts.OnOff(settings.Autoplay));
    }

    /// <summary>Asks about the music setting <paramref name="choice"/>; any other choice leaves the settings as they are.</summary>
    public AppSettings ChangeMusic(string choice, AppSettings settings)
    {
        return choice switch
        {
            KeyChoice => AskKey(settings),
            StyleChoice => settings with { HarmonyStyle = AskName<HarmonyStyle>("Vilken stil?", settings.HarmonyStyle, HarmonyNames.Style) },
            SoundChoice => settings with { Sound = AskName<Sound>("Vilket ljud?", settings.Sound) },
            RhythmChoice => AskRhythm(settings),
            ChordChangesChoice => AskChordChanges(settings),
            FeaturesChoice => AskFeatures(settings),
            AutoplayChoice => settings with { Autoplay = AskOnOff("Spela vidare i tystnad?", settings.Autoplay) },
            _ => settings,
        };
    }

    public AppSettings Recording(AppSettings settings)
    {
        return settings with { RecordOn = AskOnOff("Spela in live-läget?", settings.RecordOn) };
    }

    private AppSettings AskKey(AppSettings settings)
    {
        string[] keys = [HarmonySettings.AutoKey, .. Enumerable.Range(0, 12).Select(PitchClass.Name)];
        string current = PitchClass.TryParse(settings.Key, out int currentTonic) ? PitchClass.Name(currentTonic) : HarmonySettings.AutoKey;
        string key = console.Prompt(new SelectionPrompt<string>().Title("Vilken tonart?").AddChoices(keys).DefaultValue(current));
        if (key == HarmonySettings.AutoKey)
        {
            return settings with { Key = key };
        }

        string mode = AskName("Vilken modus?", settings.Mode, (Mode value) => $"{HarmonyNames.Mode(value)} ({value})");
        return settings with { Key = key, Mode = mode };
    }

    private AppSettings AskRhythm(AppSettings settings)
    {
        string pattern = AskName<RhythmPatternName>("Vilket rytmmönster?", settings.RhythmPattern);
        int bpm = AskInRange("Tempo (BPM):", settings.Bpm, RhythmSettings.MinBpm, RhythmSettings.MaxBpm, BpmRangeMessage);
        string timeSignature = console.Prompt(new SelectionPrompt<string>()
            .Title("Vilken taktart?")
            .AddChoices(RhythmSettings.AllTimeSignatures)
            .DefaultValue(settings.TimeSignature));
        return settings with { RhythmPattern = pattern, Bpm = bpm, TimeSignature = timeSignature };
    }

    private AppSettings AskChordChanges(AppSettings settings)
    {
        string perBar = console.Prompt(new SelectionPrompt<string>()
            .Title("Hur många ackordbyten per takt?")
            .AddChoices(ChordChangeSettings.AllPerBar)
            .DefaultValue(settings.ChordChangesPerBar)
            .UseConverter(value => ChordChangeSettings.TryParsePerBar(value, out ChordChangesPerBar parsed) ? SettingTexts.ChangesPerBar(parsed) : value));
        string sensitivity = AskName<ChordChangeSensitivity>("Hur känsligt för nya toner?", settings.ChordChangeSensitivity, SettingTexts.Sensitivity);
        return settings with { ChordChangesPerBar = perBar, ChordChangeSensitivity = sensitivity };
    }

    private AppSettings AskFeatures(AppSettings settings)
    {
        MultiSelectionPrompt<LevelFeatureEntry> prompt = new MultiSelectionPrompt<LevelFeatureEntry>()
            .Title("Vilka nivåfunktioner ska vara på? (mellanslag väljer, Enter sparar)")
            .NotRequired()
            .AddChoices(LevelFeatures.All)
            .UseConverter(entry => entry.DisplayName);
        foreach (LevelFeatureEntry on in LevelFeatures.All.Where(entry => entry.IsOn(settings)))
        {
            prompt.Select(on);
        }

        List<LevelFeatureEntry> chosen = console.Prompt(prompt);
        return LevelFeatures.All.Aggregate(settings, (switched, entry) => entry.Switch(switched, chosen.Contains(entry)));
    }

    /// <summary>Monitoring: on or off, then its level.</summary>
    public AppSettings Monitor(AppSettings settings)
    {
        bool on = AskOnOff("Höra mikrofonen i hörlurarna?", settings.MonitorOn);
        if (!on)
        {
            return settings with { MonitorOn = false };
        }

        int level = AskInRange("Medhörningsnivå (%):", settings.MonitorLevelPercent, MonitorSettings.MinLevelPercent, MonitorSettings.MaxLevelPercent, MonitorLevelMessage);
        return settings with { MonitorOn = true, MonitorLevelPercent = level };
    }

    // A whole number from minimum to maximum, starting on the current one; a value out of range is asked again.
    private int AskInRange(string title, int current, int minimum, int maximum, string outOfRange)
    {
        string error = $"[red]{outOfRange}[/]";
        return console.Prompt(new TextPrompt<int>(title)
            .DefaultValue(current)
            .ValidationErrorMessage(error)
            .Validate(value => value >= minimum && value <= maximum, error));
    }

    private bool AskOnOff(string title, bool current)
    {
        return console.Prompt(new SelectionPrompt<bool>()
            .Title(title)
            .AddChoices(true, false)
            .DefaultValue(current)
            .UseConverter(SettingTexts.OnOff));
    }

    // A choice among an enum's members, starting on the current one and saved by the member's name.
    private string AskName<TEnum>(string title, string current, Func<TEnum, string>? display = null)
        where TEnum : struct, Enum
    {
        TEnum[] values = Enum.GetValues<TEnum>();
        TEnum chosen = console.Prompt(new SelectionPrompt<TEnum>()
            .Title(title)
            .AddChoices(values)
            .DefaultValue(SettingNames.TryParse(current, out TEnum parsed) ? parsed : values[0])
            .UseConverter(value => Markup.Escape(display?.Invoke(value) ?? value.ToString())));
        return chosen.ToString();
    }
}
