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
    [InlineData("--input", "USB Audio", "--input gäller bara tillsammans med --passthrough eller --devices.")]
    [InlineData("--output", "Externa hörlurar", "--output gäller bara tillsammans med --passthrough, --devices eller --play.")]
    public void DeviceOptionWithoutPassThroughOrDevices_FailsWithSwedishMessage(string option, string name, string message)
    {
        (int exitCode, string output, string error) = Invoke(option, name);

        Assert.Equal(1, exitCode);
        Assert.Equal($"{message} {AppCommand.HelpHint}", error.Trim());
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
        Assert.Equal($"{option} gäller bara tillsammans med --harmonize eller --accompany. {AppCommand.HelpHint}", error.Trim());
        Assert.Empty(output);
    }

    [Theory]
    [InlineData("--passthrough")]
    [InlineData("--devices")]
    public void HarmonizeWithPassThroughOrDevices_FailsWithSwedishMessage(string option)
    {
        (int exitCode, string output, string error) = Invoke("--harmonize", "melodi.wav", option);

        Assert.Equal(1, exitCode);
        Assert.Equal($"{AppCommand.CombinedMessage(option, "--harmonize")} {AppCommand.HelpHint}", error.Trim());
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

    [Fact]
    public void Help_ListsSynthOptionsWithSwedishDescriptions()
    {
        (int exitCode, string output, _) = Invoke("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--render <wav>", output);
        Assert.Contains("Spela ackord med syntten och skriv dem till en WAV-fil.", output);
        Assert.Contains("--play", output);
        Assert.Contains("Spela ackord med syntten i utgången.", output);
        Assert.Contains("--sound", output);
        Assert.Contains("Syntens ljud att använda den här gången: Pad, Organ eller Pluck.", output);
        Assert.Contains("--chords", output);
        Assert.Contains("--chord-ms", output);
    }

    [Theory]
    [InlineData("--sound", "Organ")]
    [InlineData("--chords", "C G")]
    [InlineData("--chord-ms", "500")]
    public void SynthOptionWithoutRenderOrPlay_FailsWithSwedishMessage(string option, string value)
    {
        (int exitCode, _, string error) = Invoke(option, value);

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"{option} gäller bara tillsammans med ", error.Trim());
        Assert.Contains("--render", error);
    }

    [Theory]
    [InlineData("--passthrough", "--render", "--render", "x.wav", "--passthrough")]
    [InlineData("--render", "--play", "--render", "x.wav", "--play")]
    [InlineData("--devices", "--play", "--play", "--devices")]
    [InlineData("--harmonize", "--play", "--play", "--harmonize", "x.wav")]
    [InlineData("--accompany", "--play", "--accompany", "x.wav", "--render", "y.wav", "--play")]
    [InlineData("--passthrough", "--devices", "--passthrough", "--devices")]
    public void TwoModes_FailWithSwedishMessage(string first, string second, params string[] args)
    {
        (int exitCode, _, string error) = Invoke(args);

        Assert.Equal(1, exitCode);
        Assert.Equal($"{AppCommand.CombinedMessage(first, second)} {AppCommand.HelpHint}", error.Trim());
    }

    [Fact]
    public void InputWithPlay_FailsWithSwedishMessage()
    {
        (int exitCode, _, string error) = Invoke("--play", "--input", "USB Audio");

        Assert.Equal(1, exitCode);
        Assert.Equal($"{AppCommand.CombinedMessage("--input", "--play")} {AppCommand.HelpHint}", error.Trim());
    }

    [Fact]
    public void ChordsWithAccompany_SaysTheyCannotBeCombined()
    {
        (int exitCode, _, string error) = Invoke("--accompany", "x.wav", "--render", "y.wav", "--chords", "C G");

        Assert.Equal(1, exitCode);
        Assert.Equal($"{AppCommand.CombinedMessage("--chords", "--accompany")} {AppCommand.HelpHint}", error.Trim());
    }

    [Fact]
    public void AccompanyWithoutRender_FailsWithSwedishMessage()
    {
        (int exitCode, _, string error) = Invoke("--accompany", "x.wav");

        Assert.Equal(1, exitCode);
        Assert.Equal($"{AppCommand.AccompanyNeedsRenderMessage} {AppCommand.HelpHint}", error.Trim());
    }

    [Theory]
    [InlineData("--features", "gate")]
    [InlineData("--no-voice")]
    public void AccompanyOptionWithoutAccompany_FailsWithSwedishMessage(params string[] args)
    {
        (int alone, _, string aloneError) = Invoke(args);
        (int withRender, _, string renderError) = Invoke([.. args, "--render", "x.wav"]);

        Assert.Equal(1, alone);
        Assert.Equal($"{AppCommand.OptionAloneMessage(args[0], ["--accompany"])} {AppCommand.HelpHint}", aloneError.Trim());
        Assert.Equal(1, withRender);
        Assert.Equal($"{AppCommand.CombinedMessage(args[0], "--render")} {AppCommand.HelpHint}", renderError.Trim());
    }

    [Fact]
    public void Help_ListsAccompanyOptionsWithSwedishDescriptions()
    {
        (int exitCode, string output, _) = Invoke("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--accompany <wav>", output);
        Assert.Contains("--no-voice", output);
        Assert.Contains("--features", output);
        Assert.Contains("gate, onsets, character, dynamics", output);
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
