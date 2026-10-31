using System.CommandLine;

using SolidBackup.Cli;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public class AppCommandTests
{
    [Fact]
    public void Help_PrintsSwedishDescriptionAndOptions()
    {
        (int exitCode, string output, _) = Invoke("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains(AppCommand.Description, output);
        Assert.Contains("--help", output);
        Assert.Contains("--version", output);
        Assert.Contains("Visa hjälp och användning.", output);
    }

    [Fact]
    public void Help_ListsPassThroughWithSwedishDescription()
    {
        (int exitCode, string output, _) = Invoke("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--passthrough", output);
        Assert.Contains("Spela upp mikrofonen direkt i utgången (för att kontrollera ljudet).", output);
    }

    [Fact]
    public void Version_PrintsProjectVersion()
    {
        (int exitCode, string output, _) = Invoke("--version");

        Assert.Equal(0, exitCode);
        Assert.Equal("0.1.0", output.Trim());
    }

    [Fact]
    public void NoArguments_RunsMenu()
    {
        TestConsole console = new TestConsole().Interactive();
        console.Input.PushKey(ConsoleKey.Enter);

        int exitCode = AppCommand.Run([], () => console);

        Assert.Equal(0, exitCode);
        Assert.Contains(MainMenu.Title, console.Output);
        Assert.Contains(MainMenu.QuitChoice, console.Output);
    }

    [Fact]
    public void UnknownOption_FailsWithSwedishMessageAndNoStackTrace()
    {
        (int exitCode, string output, string error) = Invoke("--nope");

        Assert.Equal(1, exitCode);
        Assert.Equal($"Okänt argument: --nope. {AppCommand.HelpHint}", error.Trim());
        Assert.Empty(output);
    }

    [Fact]
    public void MenuThrows_FailsWithSwedishMessageAndNoStackTrace()
    {
        StringWriter error = new();
        InvocationConfiguration configuration = new() { Output = new StringWriter(), Error = error };

        int exitCode = AppCommand.Run([], () => throw new IOException("terminalen stängdes"), configuration);

        Assert.Equal(1, exitCode);
        Assert.Equal("Ett oväntat fel inträffade: terminalen stängdes", error.ToString().Trim());
    }

    private static (int ExitCode, string Output, string Error) Invoke(params string[] args)
    {
        StringWriter output = new();
        StringWriter error = new();
        InvocationConfiguration configuration = new() { Output = output, Error = error };

        int exitCode = AppCommand.Run(args, () => new TestConsole(), configuration);

        return (exitCode, output.ToString(), error.ToString());
    }
}
