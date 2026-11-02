using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Cli;
using SolidBackup.Configuration;
using SolidBackup.Tests.Audio.Devices;

using Spectre.Console.Testing;

namespace SolidBackup.Tests.Cli;

public class DeviceListCommandTests
{
    [Fact]
    public void Run_ListsDevicesWithDefaultsChoicesAndSpeakers()
    {
        TestConsole console = TestConsoles.Wide();

        int exitCode = Run(console, inputDevice: "usb audio", outputDevice: "");

        Assert.Equal(0, exitCode);
        Assert.Equal(
            """
            Ingångar:
              MacBook Pro-mikrofon (1 kanal) [standard]
              USB Audio (2 kanaler) [vald]
            Utgångar:
              USB Audio (2 kanaler)
              Externa hörlurar (2 kanaler) [standard] [vald]
              Högtalare i MacBook Pro (2 kanaler) [inbyggda högtalare]
            """,
            console.Output.TrimEnd().Replace("\r", ""));
    }

    [Fact]
    public void Run_ChosenDeviceMissing_SaysSoAndStillLists()
    {
        TestConsole console = TestConsoles.Wide();

        int exitCode = Run(console, inputDevice: "", outputDevice: "AirPods");

        Assert.Equal(0, exitCode);
        Assert.Contains("Det finns ingen ljudutgång som heter \"AirPods\".", console.Output);
    }

    [Fact]
    public void Run_AudioSystemFails_ShowsSwedishErrorAndExitsOne()
    {
        TestConsole console = new();

        int exitCode = new DeviceListCommand(
            console,
            () => new(Settings("", ""), null),
            () => throw new AudioDeviceException("Ljudsystemet PortAudio kunde inte startas."),
            TestPreflight.Create()).Run();

        Assert.Equal(1, exitCode);
        Assert.Contains("Ljudfel: Ljudsystemet PortAudio kunde inte startas.", console.Output);
    }

    private static int Run(TestConsole console, string inputDevice, string outputDevice)
    {
        FakeCatalog catalog = new();
        int exitCode = new DeviceListCommand(
            console, () => new(Settings(inputDevice, outputDevice), null), () => catalog, TestPreflight.Create(routes: SpeakerRoutes)).Run();
        Assert.True(catalog.Disposed);
        return exitCode;
    }

    private static readonly FakeRoutes SpeakerRoutes =
        new(name => name == TestDevices.Speakers.Name ? TestPreflight.Speakers : TestPreflight.HeadphoneJack);

    private static AppSettings Settings(string inputDevice, string outputDevice) =>
        TestSettings.Default with { InputDevice = inputDevice, OutputDevice = outputDevice };
}
