using SolidBackup.Audio;
using SolidBackup.Audio.Devices;

namespace SolidBackup.Tests.Audio.Devices;

public class DeviceSelectorTests
{
    [Fact]
    public void Select_ExactName_ReturnsThatDevice()
    {
        Assert.Equal(TestDevices.Speakers, DeviceSelector.Select(TestDevices.All, "Högtalare i MacBook Pro", AudioDirection.Output));
    }

    [Fact]
    public void Select_NameInOtherCase_ReturnsThatDevice()
    {
        Assert.Equal(TestDevices.UsbInterface, DeviceSelector.Select(TestDevices.All, "usb AUDIO", AudioDirection.Input));
    }

    [Fact]
    public void Select_DecomposedName_MatchesComposedInput()
    {
        AudioDeviceInfo decomposed = TestDevices.Headphones with { Name = "Externa ho\u0308rlurar" };

        Assert.Equal(decomposed, DeviceSelector.Select([decomposed], "Externa hörlurar", AudioDirection.Output));
    }

    [Theory]
    [InlineData(AudioDirection.Input, "MacBook Pro-mikrofon")]
    [InlineData(AudioDirection.Output, "Externa hörlurar")]
    public void Select_EmptyName_ReturnsSystemDefault(AudioDirection direction, string expected)
    {
        Assert.Equal(expected, DeviceSelector.Select(TestDevices.All, "", direction).Name);
    }

    [Fact]
    public void Select_NameOfWrongDirection_IsNotAMatch()
    {
        AudioDeviceException exception = Assert.Throws<AudioDeviceException>(
            () => DeviceSelector.Select(TestDevices.All, "Externa hörlurar", AudioDirection.Input));

        Assert.Equal(
            "Det finns ingen ljudingång som heter \"Externa hörlurar\". Tillgängliga: MacBook Pro-mikrofon, USB Audio.",
            exception.Message);
    }

    [Fact]
    public void Select_UnknownName_NamesItAndListsTheOutputs()
    {
        AudioDeviceException exception = Assert.Throws<AudioDeviceException>(
            () => DeviceSelector.Select(TestDevices.All, "AirPods", AudioDirection.Output));

        Assert.Equal(
            "Det finns ingen ljudutgång som heter \"AirPods\". Tillgängliga: USB Audio, Externa hörlurar, Högtalare i MacBook Pro.",
            exception.Message);
    }

    [Fact]
    public void Select_NoDefaultAndNoDevices_SaysSo()
    {
        AudioDeviceException exception = Assert.Throws<AudioDeviceException>(
            () => DeviceSelector.Select([TestDevices.Headphones], "", AudioDirection.Input));

        Assert.Equal("Det finns ingen ljudingång som är systemets standard. Det finns inga alls.", exception.Message);
    }
}
