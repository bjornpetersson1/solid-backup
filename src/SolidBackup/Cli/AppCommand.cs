using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Reflection;

using SolidBackup.Audio.Devices;
using SolidBackup.Audio.MacOS;
using SolidBackup.Audio.PortAudio;
using SolidBackup.Configuration;

using Spectre.Console;

namespace SolidBackup.Cli;

public static class AppCommand
{
    public const string Description = "Lyssnar på en melodistämma i mikrofonen och spelar ackord till den.";
    public const string HelpHint = "Kör med --help för att se vad som finns.";
    public const string DeviceOptionsAloneMessage = "--input och --output gäller bara tillsammans med --passthrough eller --devices.";
    public const string HarmonyOptionsAloneMessage = "--style, --key och --mode gäller bara tillsammans med --harmonize.";
    public const string HarmonizeCombinedMessage = "--harmonize kan inte kombineras med --passthrough eller --devices.";

    /// <summary>
    /// Parses and runs the command line. Parse errors and unexpected exceptions get a Swedish
    /// message and exit code 1, instead of System.CommandLine's English text or a stack trace.
    /// </summary>
    public static int Run(string[] args, Func<IAnsiConsole> createConsole, InvocationConfiguration? configuration = null)
    {
        configuration ??= new InvocationConfiguration();
        configuration.EnableDefaultExceptionHandler = false;
        ParseResult parseResult = Create(createConsole).Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            configuration.Error.WriteLine($"{ParseErrorMessage(parseResult)} {HelpHint}");
            return 1;
        }

        try
        {
            return parseResult.Invoke(configuration);
        }
        catch (Exception exception)
        {
            configuration.Error.WriteLine($"Ett oväntat fel inträffade: {exception.Message}");
            return 1;
        }
    }

    public static RootCommand Create(Func<IAnsiConsole> createConsole)
    {
        RootCommand root = new(Description);
        root.Options.OfType<HelpOption>().Single().Description = "Visa hjälp och användning.";
        VersionOption versionOption = root.Options.OfType<VersionOption>().Single();
        versionOption.Description = "Visa versionen.";
        versionOption.Action = new PrintVersionAction();
        Option<bool> passThroughOption = new("--passthrough")
        {
            Description = "Spela upp mikrofonen direkt i utgången (för att kontrollera ljudet).",
        };
        Option<bool> devicesOption = new("--devices")
        {
            Description = "Lista ljudenheterna och visa vilka som är valda.",
        };
        Option<string> inputOption = new("--input")
        {
            Description = "Ljudingång att använda den här gången (enhetens namn).",
        };
        Option<string> outputOption = new("--output")
        {
            Description = "Ljudutgång att använda den här gången (enhetens namn).",
        };
        Option<string> harmonizeOption = new("--harmonize")
        {
            Description = "Kör tonhöjdsanalys och harmoni på en WAV-fil och skriv ut ackordbytena.",
            HelpName = "wav",
        };
        Option<string> styleOption = new("--style")
        {
            Description = "Harmonistil att använda den här gången: Church, Campfire, Drone, Pop, Modal, Blues eller Free.",
        };
        Option<string> keyOption = new("--key")
        {
            Description = "Tonart att använda den här gången: en grundton (t.ex. C, F#, Bb) eller auto.",
        };
        Option<string> modeOption = new("--mode")
        {
            Description = "Modus att använda den här gången (t.ex. Ionian, Dorian, Aeolian).",
        };
        root.Options.Add(passThroughOption);
        root.Options.Add(devicesOption);
        root.Options.Add(inputOption);
        root.Options.Add(outputOption);
        root.Options.Add(harmonizeOption);
        root.Options.Add(styleOption);
        root.Options.Add(keyOption);
        root.Options.Add(modeOption);
        root.SetAction(parseResult =>
        {
            bool playsOrLists = parseResult.GetValue(devicesOption) || parseResult.GetValue(passThroughOption);
            if (!playsOrLists && (parseResult.GetValue(inputOption) is not null || parseResult.GetValue(outputOption) is not null))
            {
                parseResult.InvocationConfiguration.Error.WriteLine($"{DeviceOptionsAloneMessage} {HelpHint}");
                return 1;
            }

            string? harmonizePath = parseResult.GetValue(harmonizeOption);
            string? style = parseResult.GetValue(styleOption);
            string? key = parseResult.GetValue(keyOption);
            string? mode = parseResult.GetValue(modeOption);
            if (harmonizePath is null && (style is not null || key is not null || mode is not null))
            {
                parseResult.InvocationConfiguration.Error.WriteLine($"{HarmonyOptionsAloneMessage} {HelpHint}");
                return 1;
            }

            if (harmonizePath is not null)
            {
                if (playsOrLists)
                {
                    parseResult.InvocationConfiguration.Error.WriteLine($"{HarmonizeCombinedMessage} {HelpHint}");
                    return 1;
                }

                return new HarmonizeCommand(createConsole(), LoadSettings).Run(harmonizePath, style, key, mode);
            }

            Func<SettingsLoadResult> loadSettings =
                () => WithDevices(LoadSettings(), parseResult.GetValue(inputOption), parseResult.GetValue(outputOption));
            if (parseResult.GetValue(devicesOption))
            {
                return new DeviceListCommand(createConsole(), loadSettings, PortAudioSession.Open, CreatePreflight()).Run();
            }

            return parseResult.GetValue(passThroughOption)
                ? new PassThroughCommand(createConsole(), loadSettings, PortAudioDevices.Open, CreatePreflight()).Run()
                : new MainMenu(createConsole()).Run();
        });
        return root;
    }

    /// <summary>Applies --input and --output for this run only; they are never saved.</summary>
    public static SettingsLoadResult WithDevices(SettingsLoadResult loaded, string? input, string? output)
    {
        AppSettings settings = loaded.Settings with
        {
            InputDevice = input ?? loaded.Settings.InputDevice,
            OutputDevice = output ?? loaded.Settings.OutputDevice,
        };
        return loaded with { Settings = settings };
    }

    private static SettingsLoadResult LoadSettings()
    {
        return new SettingsStore(SettingsStore.BundledDefaultsPath, SettingsStore.DefaultUserSettingsPath).Load();
    }

    private static AudioPreflight CreatePreflight()
    {
        return new AudioPreflight(
            new AvFoundationPermissionSource(),
            new CoreAudioRouteSource(),
            AudioPreflight.TerminalName(Environment.GetEnvironmentVariable("TERM_PROGRAM")));
    }

    private static string ParseErrorMessage(ParseResult parseResult)
    {
        return parseResult.UnmatchedTokens.Count > 0
            ? $"Okänt argument: {string.Join(", ", parseResult.UnmatchedTokens)}."
            : "Argumenten kunde inte tolkas.";
    }

    // The built-in version action reads the entry assembly, which is the test host under tests.
    private sealed class PrintVersionAction : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            string version = typeof(AppCommand).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "okänd";
            parseResult.InvocationConfiguration.Output.WriteLine(version);
            return 0;
        }
    }
}
