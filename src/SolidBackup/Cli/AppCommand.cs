using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Reflection;

using SolidBackup.Accompaniment;
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
    public const string AccompanyNeedsRenderMessage = "--accompany kräver --render <wav> för resultatet.";

    /// <summary>
    /// Parses and runs the command line. Parse errors and unexpected exceptions get a Swedish
    /// message and exit code 1, instead of System.CommandLine's English text or a stack trace.
    /// </summary>
    /// <param name="createMenu">Builds the menu shown without arguments; the real devices and settings when null.</param>
    public static int Run(
        string[] args, Func<IAnsiConsole> createConsole, InvocationConfiguration? configuration = null, Func<IAnsiConsole, MainMenu>? createMenu = null)
    {
        configuration ??= new InvocationConfiguration();
        configuration.EnableDefaultExceptionHandler = false;
        ParseResult parseResult = Create(createConsole, createMenu).Parse(args);
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

    public static RootCommand Create(Func<IAnsiConsole> createConsole, Func<IAnsiConsole, MainMenu>? createMenu = null)
    {
        createMenu ??= CreateMenu;
        RootCommand root = new(Description);
        root.Options.OfType<HelpOption>().Single().Description = "Visa hjälp och användning.";
        VersionOption versionOption = root.Options.OfType<VersionOption>().Single();
        versionOption.Description = "Visa versionen.";
        versionOption.Action = new PrintVersionAction();
        Option<bool> passThroughOption = new("--passthrough")
        {
            Description = "Spela upp mikrofonen direkt i utgången (för att kontrollera ljudet).",
        };
        Option<bool> liveOption = new("--live")
        {
            Description = "Spela live: mikrofonen genom analys, harmoni och synt till utgången, tills q eller Esc.",
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
            Description = "Harmonistil att använda den här gången: Church, Campfire, Drone, Pop, Modal, Blues, Jazz eller Free.",
        };
        Option<string> keyOption = new("--key")
        {
            Description = "Tonart att använda den här gången: en grundton (t.ex. C, F#, Bb) eller auto.",
        };
        Option<string> modeOption = new("--mode")
        {
            Description = "Modus att använda den här gången (t.ex. Ionian, Dorian, Aeolian).",
        };
        Option<string> renderOption = new("--render")
        {
            Description = "Spela ackord med syntten och skriv dem till en WAV-fil.",
            HelpName = "wav",
        };
        Option<bool> playOption = new("--play")
        {
            Description = "Spela ackord med syntten i utgången.",
        };
        Option<string> soundOption = new("--sound")
        {
            Description = "Syntens ljud att använda den här gången: Pad, Organ eller Pluck.",
        };
        Option<string> chordsOption = new("--chords")
        {
            Description = $"Ackorden att spela, med mellanslag emellan (standard \"{SynthCommand.DefaultChords}\").",
        };
        Option<int?> chordMsOption = new("--chord-ms")
        {
            Description = $"Hur länge varje ackord spelas, {SynthCommand.MinChordMs}–{SynthCommand.MaxChordMs} ms (standard {SynthCommand.DefaultChordMs}).",
        };
        Option<string> accompanyOption = new("--accompany")
        {
            Description = "Kör en WAV-fil genom analys, nivåfunktioner, harmoni och synt och skriv resultatet med --render.",
            HelpName = "wav",
        };
        Option<string> patternOption = new("--pattern")
        {
            Description = "Rytmmönster att använda den här gången: Held, Arpeggio eller Strum.",
        };
        Option<int?> bpmOption = new("--bpm")
        {
            Description = $"Tempo att använda den här gången, {Rhythm.RhythmSettings.MinBpm}–{Rhythm.RhythmSettings.MaxBpm} BPM.",
        };
        Option<string> timeSignatureOption = new("--time-signature")
        {
            Description = "Taktart att använda den här gången: 4/4 eller 3/4.",
        };
        Option<bool> recordOption = new("--record")
        {
            Description = "Spela in ingången och utgången till en WAV-fil i inspelningsmappen (med --passthrough eller --live).",
        };
        Option<bool> noVoiceOption = new("--no-voice")
        {
            Description = "Skriv bara ackompanjemanget, utan rösten (med --accompany).",
        };
        Option<string> featuresOption = new("--features")
        {
            Description = $"Nivåfunktioner att använda den här gången, med kommatecken emellan: {string.Join(", ", LevelFeatures.All.Select(entry => entry.Name))}, eller {SettingOverrides.NoFeatures}.",
        };
        root.Options.Add(passThroughOption);
        root.Options.Add(liveOption);
        root.Options.Add(devicesOption);
        root.Options.Add(inputOption);
        root.Options.Add(outputOption);
        root.Options.Add(harmonizeOption);
        root.Options.Add(styleOption);
        root.Options.Add(keyOption);
        root.Options.Add(modeOption);
        root.Options.Add(renderOption);
        root.Options.Add(playOption);
        root.Options.Add(soundOption);
        root.Options.Add(chordsOption);
        root.Options.Add(chordMsOption);
        root.Options.Add(accompanyOption);
        root.Options.Add(noVoiceOption);
        root.Options.Add(recordOption);
        root.Options.Add(patternOption);
        root.Options.Add(bpmOption);
        root.Options.Add(timeSignatureOption);
        root.Options.Add(featuresOption);
        // Each way of running and the options that go with it; an option belongs to the modes that list it.
        RunMode[] modes =
        [
            new(passThroughOption, [inputOption, outputOption, recordOption]),
            new(liveOption, [inputOption, outputOption, styleOption, keyOption, modeOption, soundOption, featuresOption, patternOption, bpmOption, timeSignatureOption, recordOption]),
            new(devicesOption, [inputOption, outputOption]),
            new(harmonizeOption, [styleOption, keyOption, modeOption, bpmOption, timeSignatureOption]),
            new(accompanyOption, [renderOption, styleOption, keyOption, modeOption, soundOption, featuresOption, noVoiceOption, patternOption, bpmOption, timeSignatureOption]),
            new(renderOption, [soundOption, chordsOption, chordMsOption, patternOption, bpmOption, timeSignatureOption]),
            new(playOption, [soundOption, chordsOption, chordMsOption, outputOption, patternOption, bpmOption, timeSignatureOption]),
        ];
        root.SetAction(parseResult =>
        {
            if (CheckOptions(parseResult, modes, accompanyOption, renderOption) is { } problem)
            {
                parseResult.InvocationConfiguration.Error.WriteLine($"{problem} {HelpHint}");
                return 1;
            }

            string? style = parseResult.GetValue(styleOption);
            string? key = parseResult.GetValue(keyOption);
            string? mode = parseResult.GetValue(modeOption);
            string? sound = parseResult.GetValue(soundOption);
            RhythmOverrides rhythm = new(parseResult.GetValue(patternOption), parseResult.GetValue(bpmOption), parseResult.GetValue(timeSignatureOption));
            SettingOverrides overrides = new(style, key, mode, sound, parseResult.GetValue(featuresOption), rhythm);
            if (parseResult.GetValue(accompanyOption) is { } recording && parseResult.GetValue(renderOption) is { } accompanied)
            {
                AccompanyOptions options = new(overrides, parseResult.GetValue(noVoiceOption));
                return new AccompanyCommand(createConsole(), LoadSettings).Run(recording, accompanied, options);
            }

            string? chords = parseResult.GetValue(chordsOption);
            int? chordMs = parseResult.GetValue(chordMsOption);
            Func<SettingsLoadResult> loadSettings =
                () => WithDevices(LoadSettings(), parseResult.GetValue(inputOption), parseResult.GetValue(outputOption));
            if (parseResult.GetValue(renderOption) is { } renderPath)
            {
                return new SynthCommand(createConsole(), loadSettings, PortAudioPlayback.Open, CreatePreflight()).Render(renderPath, sound, chords, chordMs, rhythm);
            }

            if (parseResult.GetValue(playOption))
            {
                return new SynthCommand(createConsole(), loadSettings, PortAudioPlayback.Open, CreatePreflight()).Play(sound, chords, chordMs, rhythm);
            }

            if (parseResult.GetValue(harmonizeOption) is { } harmonizePath)
            {
                return new HarmonizeCommand(createConsole(), LoadSettings).Run(harmonizePath, style, key, mode, rhythm);
            }

            if (parseResult.GetValue(liveOption))
            {
                return new LiveCommand(createConsole(), loadSettings, PortAudioDevices.Open, CreatePreflight())
                    .Run(overrides, record: parseResult.GetValue(recordOption));
            }

            if (parseResult.GetValue(devicesOption))
            {
                return new DeviceListCommand(createConsole(), loadSettings, PortAudioSession.Open, CreatePreflight()).Run();
            }

            return parseResult.GetValue(passThroughOption)
                ? new PassThroughCommand(createConsole(), loadSettings, PortAudioDevices.Open, CreatePreflight()).Run(record: parseResult.GetValue(recordOption))
                : createMenu(createConsole()).Run();
        });
        return root;
    }

    public static string CombinedMessage(string first, string second)
    {
        return $"{first} kan inte kombineras med {second}.";
    }

    public static string OptionAloneMessage(string option, IEnumerable<string> modes)
    {
        List<string> names = [.. modes];
        string allowed = names.Count == 1 ? names[0] : $"{string.Join(", ", names[..^1])} eller {names[^1]}";
        return $"{option} gäller bara tillsammans med {allowed}.";
    }

    /// <summary>
    /// At most one mode, and every other option given belongs to it; <c>--render</c> is the output of
    /// <c>--accompany</c> rather than a mode of its own when both are given. Null when the options fit together.
    /// </summary>
    private static string? CheckOptions(ParseResult parseResult, RunMode[] modes, Option accompanyOption, Option renderOption)
    {
        bool accompanies = Given(parseResult, accompanyOption);
        List<RunMode> chosen = [.. modes.Where(mode => Given(parseResult, mode.Option) && !(accompanies && mode.Option == renderOption))];
        if (chosen.Count > 1)
        {
            return CombinedMessage(chosen[0].Option.Name, chosen[1].Option.Name);
        }

        RunMode? running = chosen.FirstOrDefault();
        foreach (Option option in modes.SelectMany(mode => mode.Modifiers).Distinct())
        {
            if (!Given(parseResult, option) || running?.Modifiers.Contains(option) == true || running?.Option == option)
            {
                continue;
            }

            return running is null
                ? OptionAloneMessage(option.Name, modes.Where(mode => mode.Modifiers.Contains(option)).Select(mode => mode.Option.Name))
                : CombinedMessage(option.Name, running.Option.Name);
        }

        return accompanies && !Given(parseResult, renderOption) ? AccompanyNeedsRenderMessage : null;
    }

    private static bool Given(ParseResult parseResult, Option option)
    {
        return parseResult.GetResult(option) is { Implicit: false };
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

    private static MainMenu CreateMenu(IAnsiConsole console)
    {
        AudioPreflight preflight = CreatePreflight();
        LiveCommand live = new(console, LoadSettings, PortAudioDevices.Open, preflight, returnsToMenu: true);
        return new MainMenu(console, CreateStore(), PortAudioSession.Open, preflight, settings => live.Play(settings, settings.RecordOn));
    }

    private static SettingsLoadResult LoadSettings()
    {
        return CreateStore().Load();
    }

    private static SettingsStore CreateStore()
    {
        return new SettingsStore(SettingsStore.BundledDefaultsPath, SettingsStore.DefaultUserSettingsPath);
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

    private sealed record RunMode(Option Option, IReadOnlyList<Option> Modifiers);

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
