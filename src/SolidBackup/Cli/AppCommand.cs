using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Reflection;

using SolidBackup.Audio.PortAudio;
using SolidBackup.Configuration;

using Spectre.Console;

namespace SolidBackup.Cli;

public static class AppCommand
{
    public const string Description = "Lyssnar på en melodistämma i mikrofonen och spelar ackord till den.";
    public const string HelpHint = "Kör med --help för att se vad som finns.";

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
        root.Options.Add(passThroughOption);
        root.SetAction(parseResult => parseResult.GetValue(passThroughOption)
            ? CreatePassThrough(createConsole()).Run()
            : new MainMenu(createConsole()).Run());
        return root;
    }

    private static PassThroughCommand CreatePassThrough(IAnsiConsole console)
    {
        SettingsStore store = new(SettingsStore.BundledDefaultsPath, SettingsStore.DefaultUserSettingsPath);
        return new PassThroughCommand(console, store.Load, PortAudioDevices.Open);
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
