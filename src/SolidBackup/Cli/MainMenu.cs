using SolidBackup.Accompaniment;
using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Rhythm;
using SolidBackup.Synthesis;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// The start menu: a table of the current choices, one item per choice, live mode and quit. Each choice is saved to
/// the user settings file as soon as it is made, so it is the next start's default. Live mode runs through
/// <paramref name="startLive"/> (<see cref="LiveCommand.Play"/>, recording when <c>RecordOn</c> is on) and comes back to the menu after q or Esc; Ctrl+C
/// quits the app, from live mode once its audio and recording have closed.
/// </summary>
public sealed class MainMenu(
    IAnsiConsole console,
    SettingsStore store,
    Func<IAudioDeviceCatalog> openCatalog,
    AudioPreflight preflight,
    Func<AppSettings, LiveExit> startLive)
{
    public const string Title = "solid-backup";
    public const string StartChoice = "Starta live";
    public const string QuitChoice = "Avsluta";
    public const string NotInteractiveMessage = "Menyn kräver en interaktiv terminal.";

    private const string InputChoice = "Ingång";
    private const string OutputChoice = "Utgång";
    private const string KeyChoice = "Tonart";
    private const string StyleChoice = "Stil";
    private const string SoundChoice = "Ljud";
    private const string RhythmChoice = "Rytm";
    private const string ChordChangesChoice = "Ackordbyten";
    private const string FeaturesChoice = "Nivåfunktioner";
    private const string AutoplayChoice = "Autospel";
    private const string RecordingChoice = "Inspelning";
    private const string SystemDefault = "Systemets standard";

    private IReadOnlyList<AudioDeviceInfo> _devices = [];
    private string? _speakerWarning;

    /// <summary>The menu's items, in the order shown; the first is preselected.</summary>
    public static IReadOnlyList<string> Choices { get; } =
        [StartChoice, InputChoice, OutputChoice, KeyChoice, StyleChoice, SoundChoice, RhythmChoice, ChordChangesChoice, FeaturesChoice, AutoplayChoice, RecordingChoice, QuitChoice];

    public static string BpmRangeMessage => $"Ange ett tempo mellan {RhythmSettings.MinBpm} och {RhythmSettings.MaxBpm} BPM.";

    /// <summary>Shows the menu until Avsluta or Ctrl+C.</summary>
    public int Run()
    {
        console.Write(new Rule($"[bold]{Title}[/]"));
        if (!console.Profile.Capabilities.Interactive)
        {
            console.MarkupLine($"[red]{NotInteractiveMessage}[/]");
            return 1;
        }

        SettingsLoadResult loaded = store.Load();
        ConsoleMessages.SettingsMessage(console, loaded);
        AppSettings settings = loaded.Settings;
        ReadDevices(settings);
        Console.CancelKeyPress += QuitOnCtrlC;
        try
        {
            while (true)
            {
                ShowChoices(settings);
                string choice = console.Prompt(new SelectionPrompt<string>().Title("Vad vill du göra?").AddChoices(Choices));
                if (choice == QuitChoice)
                {
                    return 0;
                }

                if (choice == StartChoice)
                {
                    LiveExit exit = Play(settings);
                    if (exit.Interrupted)
                    {
                        return exit.ExitCode;
                    }

                    ReadDevices(settings);
                    continue;
                }

                if (choice is InputChoice or OutputChoice)
                {
                    ReadDevices(settings);
                }

                AppSettings changed = Change(choice, settings);
                if (changed != settings)
                {
                    settings = changed;
                    Save(settings);
                    CheckSpeakers(settings);
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= QuitOnCtrlC;
        }
    }

    // In the menu's prompts nothing is open and every choice is already saved, so Ctrl+C ends the app at once. Live
    // mode handles Ctrl+C itself, so the menu stops listening while it runs.
    private void QuitOnCtrlC(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        console.Cursor.Show();
        Environment.Exit(0);
    }

    private LiveExit Play(AppSettings settings)
    {
        Console.CancelKeyPress -= QuitOnCtrlC;
        try
        {
            return startLive(settings);
        }
        finally
        {
            Console.CancelKeyPress += QuitOnCtrlC;
        }
    }

    private void Save(AppSettings settings)
    {
        try
        {
            store.Save(settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ConsoleMessages.Warning(console, $"Valet kunde inte sparas ({exception.Message}); det gäller ändå den här gången.");
        }
    }

    // Read when the menu starts, before a device choice and after live mode, so a device plugged in meanwhile appears
    // and the speaker check follows a default output that changed.
    private void ReadDevices(AppSettings settings)
    {
        try
        {
            using IAudioDeviceCatalog catalog = openCatalog();
            _devices = catalog.Devices;
        }
        catch (AudioDeviceException exception)
        {
            ConsoleMessages.AudioError(console, exception);
            _devices = [];
        }

        CheckSpeakers(settings);
    }

    private void CheckSpeakers(AppSettings settings)
    {
        _speakerWarning = DeviceSelector.Find(_devices, settings.OutputDevice, AudioDirection.Output) is { } output
            ? preflight.SpeakerProblem(output.Name)
            : null;
    }

    private void ShowChoices(AppSettings settings)
    {
        Table table = new Table().AddColumn("Inställning").AddColumn("Val");
        table.AddRow(InputChoice, Markup.Escape(DeviceText(settings.InputDevice, AudioDirection.Input)));
        table.AddRow(OutputChoice, Markup.Escape(DeviceText(settings.OutputDevice, AudioDirection.Output)));
        table.AddRow(KeyChoice, Markup.Escape(SettingTexts.Key(settings)));
        table.AddRow(StyleChoice, Markup.Escape(SettingTexts.Style(settings)));
        table.AddRow(SoundChoice, Markup.Escape(settings.Sound));
        table.AddRow(RhythmChoice, Markup.Escape(SettingTexts.Rhythm(settings)));
        table.AddRow(ChordChangesChoice, Markup.Escape(SettingTexts.ChordChanges(settings)));
        table.AddRow(FeaturesChoice, Markup.Escape(SettingTexts.Features(settings)));
        table.AddRow(AutoplayChoice, SettingTexts.OnOff(settings.Autoplay));
        table.AddRow(RecordingChoice, SettingTexts.OnOff(settings.RecordOn));
        console.Write(table);
        if (_speakerWarning is { } warning)
        {
            ConsoleMessages.Warning(console, warning);
        }
    }

    // A saved name, or the system default for an empty one, marked when no such device is there.
    private string DeviceText(string name, AudioDirection direction)
    {
        string shown = name.Length == 0 ? SystemDefault : name;
        return DeviceSelector.Find(_devices, name, direction) is null ? $"{shown} (saknas)" : shown;
    }

    private AppSettings Change(string choice, AppSettings settings)
    {
        return choice switch
        {
            InputChoice => settings with { InputDevice = AskDevice(AudioDirection.Input, settings.InputDevice) },
            OutputChoice => settings with { OutputDevice = AskDevice(AudioDirection.Output, settings.OutputDevice) },
            KeyChoice => AskKey(settings),
            StyleChoice => settings with { HarmonyStyle = AskName<HarmonyStyle>("Vilken stil?", settings.HarmonyStyle, HarmonyNames.Style) },
            SoundChoice => settings with { Sound = AskName<Sound>("Vilket ljud?", settings.Sound) },
            RhythmChoice => AskRhythm(settings),
            ChordChangesChoice => AskChordChanges(settings),
            FeaturesChoice => AskFeatures(settings),
            AutoplayChoice => settings with { Autoplay = AskOnOff("Spela vidare i tystnad?", settings.Autoplay) },
            RecordingChoice => settings with { RecordOn = AskOnOff("Spela in live-läget?", settings.RecordOn) },
            _ => settings,
        };
    }

    // Saved as the device's name, or empty for the system default.
    private string AskDevice(AudioDirection direction, string current)
    {
        // Names are matched as the devices are chosen (ignoring case and Unicode form), so a saved name starts on its
        // device's row; a saved device that is gone gets a row of its own.
        AudioDeviceInfo? saved = current.Length == 0 ? null : DeviceSelector.Find(_devices, current, direction);
        List<string> names = [string.Empty, .. _devices.Where(device => device.Supports(direction)).Select(device => device.Name)];
        if (current.Length > 0 && saved is null)
        {
            names.Add(current);
        }

        AudioDeviceInfo? systemDefault = DeviceSelector.Find(_devices, string.Empty, direction);
        return console.Prompt(new SelectionPrompt<string>()
            .Title(direction == AudioDirection.Input ? "Vilken ingång?" : "Vilken utgång?")
            .AddChoices(names)
            .DefaultValue(saved?.Name ?? current)
            .UseConverter(name => Markup.Escape(
                name.Length > 0 && name == systemDefault?.Name ? $"{name} {DeviceListCommand.DefaultTag}" : DeviceText(name, direction))));
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
        int bpm = console.Prompt(new TextPrompt<int>("Tempo (BPM):")
            .DefaultValue(settings.Bpm)
            .ValidationErrorMessage($"[red]{BpmRangeMessage}[/]")
            .Validate(value => value is >= RhythmSettings.MinBpm and <= RhythmSettings.MaxBpm, $"[red]{BpmRangeMessage}[/]"));
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
