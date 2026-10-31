using System.CommandLine;

using SolidBackup.Cli;
using SolidBackup.Configuration;

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
    public void Help_ListsDeviceOptionsWithSwedishDescriptions()
    {
        (int exitCode, string output, _) = Invoke("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--devices", output);
        Assert.Contains("Lista ljudenheterna och visa vilka som är valda.", output);
        Assert.Contains("--input", output);
        Assert.Contains("Ljudingång att använda den här gången (enhetens namn).", output);
        Assert.Contains("--output", output);
        Assert.Contains("Ljudutgång att använda den här gången (enhetens namn).", output);
    }

    [Theory]
    [InlineData("--input", "USB Audio")]
    [InlineData("--output", "Externa hörlurar")]
    public void DeviceOptionWithoutPassThroughOrDevices_FailsWithSwedishMessage(string option, string name)
    {
        (int exitCode, string output, string error) = Invoke(option, name);

        Assert.Equal(1, exitCode);
        Assert.Equal($"{AppCommand.DeviceOptionsAloneMessage} {AppCommand.HelpHint}", error.Trim());
        Assert.Empty(output);
    }

    [Fact]
    public void WithDevices_OverridesOnlyTheGivenDevicesAndKeepsTheMessage()
    {
        SettingsLoadResult loaded = new(TestSettings.Default with { InputDevice = "USB Audio" }, "meddelande");

        SettingsLoadResult result = AppCommand.WithDevices(loaded, input: null, output: "Externa hörlurar");

        Assert.Equal("USB Audio", result.Settings.InputDevice);
        Assert.Equal("Externa hörlurar", result.Settings.OutputDevice);
        Assert.Equal("meddelande", result.Message);
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
    public void Help_ListsHarmonizeOptionsWithSwedishDescriptions()
    {
        (int exitCode, string output, _) = Invoke("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--harmonize <wav>", output);
        Assert.Contains("Kör tonhöjdsanalys och harmoni på en WAV-fil och skriv ut ackordbytena.", output);
        Assert.Contains("--style", output);
        Assert.Contains("--key", output);
        Assert.Contains("--mode", output);
    }

    [Theory]
    [InlineData("--style", "Campfire")]
    [InlineData("--key", "G")]
    [InlineData("--mode", "Dorian")]
    public void HarmonyOptionWithoutHarmonize_FailsWithSwedishMessage(string option, string value)
    {
        (int exitCode, string output, string error) = Invoke(option, value);

        Assert.Equal(1, exitCode);
        Assert.Equal($"{AppCommand.HarmonyOptionsAloneMessage} {AppCommand.HelpHint}", error.Trim());
        Assert.Empty(output);
    }

    [Theory]
    [InlineData("--passthrough")]
    [InlineData("--devices")]
    public void HarmonizeWithPassThroughOrDevices_FailsWithSwedishMessage(string option)
    {
        (int exitCode, string output, string error) = Invoke("--harmonize", "melodi.wav", option);

        Assert.Equal(1, exitCode);
        Assert.Equal($"{AppCommand.HarmonizeCombinedMessage} {AppCommand.HelpHint}", error.Trim());
        Assert.Empty(output);
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
