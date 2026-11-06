using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Configuration;
using SolidBackup.Live;

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
    Func<AppSettings, LiveExit> startLive,
    Action<AppSettings> openRecordings,
    Action<AppSettings> openImport)
{
    public const string Title = "solid-backup";
    public const string StartChoice = "Starta live";
    public const string QuitChoice = "Avsluta";
    public const string NotInteractiveMessage = "Menyn kräver en interaktiv terminal.";

    private const string InputChoice = "Ingång";
    private const string OutputChoice = "Utgång";
    private const string MonitorChoice = "Medhörning";
    private const string RecordingChoice = "Inspelning";
    private const string RecordingsChoice = "Inspelningar";
    private const string ImportChoice = "Importera klipp";
    private const string SystemDefault = "Systemets standard";

    private readonly SettingPrompts _prompts = new(console);
    private IReadOnlyList<AudioDeviceInfo> _devices = [];
    private string? _speakerWarning;
    private bool _outputSafe;

    /// <summary>The menu's items, in the order shown; the first is preselected.</summary>
    public static IReadOnlyList<string> Choices { get; } =
        [StartChoice, InputChoice, OutputChoice, .. SettingPrompts.MusicChoices, MonitorChoice, RecordingChoice, RecordingsChoice, ImportChoice, QuitChoice];


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

                if (choice == RecordingsChoice)
                {
                    openRecordings(settings);
                    continue;
                }

                if (choice == ImportChoice)
                {
                    // The import handles Ctrl+C itself, so a conversion or a half-written result is cleaned up.
                    Console.CancelKeyPress -= QuitOnCtrlC;
                    try
                    {
                        openImport(settings);
                    }
                    finally
                    {
                        Console.CancelKeyPress += QuitOnCtrlC;
                    }

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
        AudioDeviceInfo? output = DeviceSelector.Find(_devices, settings.OutputDevice, AudioDirection.Output);
        OutputCheck check = output is null ? default : preflight.CheckOutput(output.Name);
        _speakerWarning = check.SpeakerWarning;
        _outputSafe = check.SafeForMonitoring;
    }

    private void ShowChoices(AppSettings settings)
    {
        Table table = new Table().AddColumn("Inställning").AddColumn("Val");
        table.AddRow(InputChoice, Markup.Escape(DeviceText(settings.InputDevice, AudioDirection.Input)));
        table.AddRow(OutputChoice, Markup.Escape(DeviceText(settings.OutputDevice, AudioDirection.Output)));
        SettingPrompts.AddMusicRows(table, settings);
        table.AddRow(MonitorChoice, Markup.Escape(SettingTexts.Monitor(settings, InputMonitor.Monitors(settings, _outputSafe))));
        table.AddRow(RecordingChoice, SettingTexts.OnOff(settings.RecordOn));
        console.Write(table);
        if (_speakerWarning is { } warning)
        {
            ConsoleMessages.Warning(console, warning);
        }

        if (settings.MonitorOn && !InputMonitor.Monitors(settings, _outputSafe))
        {
            ConsoleMessages.Warning(console, SettingTexts.MonitorOffUnsafeOutput);
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
            MonitorChoice => _prompts.Monitor(settings),
            RecordingChoice => _prompts.Recording(settings),
            _ => _prompts.ChangeMusic(choice, settings),
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
}
