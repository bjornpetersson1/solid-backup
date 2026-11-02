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
    [InlineData("--input", "USB Audio", "--input gäller bara tillsammans med --passthrough, --live eller --devices.")]
    [InlineData("--output", "Externa hörlurar", "--output gäller bara tillsammans med --passthrough, --live, --devices eller --play.")]
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
    public void Help_ListsLiveWithSwedishDescription()
    {
        (int exitCode, string output, _) = Invoke("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--live", output);
        Assert.Contains("Spela live: mikrofonen genom analys, harmoni och synt till utgången, tills q eller Esc.", output);
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
        Assert.Equal($"{option} gäller bara tillsammans med --live, --harmonize eller --accompany. {AppCommand.HelpHint}", error.Trim());
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
    [InlineData("--passthrough", "--live", "--live", "--passthrough")]
    [InlineData("--live", "--harmonize", "--live", "--harmonize", "x.wav")]
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
    public void RecordWithoutPassThrough_FailsWithSwedishMessage()
    {
        (int exitCode, _, string error) = Invoke("--record");

        Assert.Equal(1, exitCode);
        Assert.Equal($"{AppCommand.OptionAloneMessage("--record", ["--passthrough", "--live"])} {AppCommand.HelpHint}", error.Trim());
    }

    [Fact]
    public void Help_ListsRecordWithSwedishDescription()
    {
        (_, string output, _) = Invoke("--help");

        Assert.Contains("--record", output);
        Assert.Contains("Spela in ingången och utgången till en WAV-fil i inspelningsmappen (med --passthrough eller --live).", output);
    }

    [Fact]
    public void Help_ListsTheRhythmOptions()
    {
        (_, string output, _) = Invoke("--help");

        Assert.Contains("--pattern", output);
        Assert.Contains("Rytmmönster att använda den här gången: Held, Arpeggio eller Strum.", output);
        Assert.Contains("--bpm", output);
        Assert.Contains("--time-signature", output);
    }

    [Theory]
    [InlineData("--pattern", "Strum", new[] { "--live", "--accompany", "--render", "--play" })]
    [InlineData("--bpm", "120", new[] { "--live", "--harmonize", "--accompany", "--render", "--play" })]
    [InlineData("--time-signature", "3/4", new[] { "--live", "--harmonize", "--accompany", "--render", "--play" })]
    public void RhythmOptionWithoutAMode_FailsWithSwedishMessage(string option, string value, string[] modes)
    {
        (int exitCode, _, string error) = Invoke(option, value);

        Assert.Equal(1, exitCode);
        Assert.Equal($"{AppCommand.OptionAloneMessage(option, modes)} {AppCommand.HelpHint}", error.Trim());
    }

    [Fact]
    public void AccompanyWithoutRender_FailsWithSwedishMessage()
    {
        (int exitCode, _, string error) = Invoke("--accompany", "x.wav");

        Assert.Equal(1, exitCode);
        Assert.Equal($"{AppCommand.AccompanyNeedsRenderMessage} {AppCommand.HelpHint}", error.Trim());
    }

    [Theory]
    [InlineData(new[] { "--live", "--accompany" }, "--features", "gate")]
    [InlineData(new[] { "--accompany" }, "--no-voice")]
    public void AccompanyOptionWithoutAccompany_FailsWithSwedishMessage(string[] modes, params string[] args)
    {
        (int alone, _, string aloneError) = Invoke(args);
        (int withRender, _, string renderError) = Invoke([.. args, "--render", "x.wav"]);

        Assert.Equal(1, alone);
        Assert.Equal($"{AppCommand.OptionAloneMessage(args[0], modes)} {AppCommand.HelpHint}", aloneError.Trim());
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
