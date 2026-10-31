using SolidBackup.Audio.Devices;

namespace SolidBackup.Tests.Audio.Devices;

internal static class TestDevices
{
    public static readonly AudioDeviceInfo Microphone = new(0, "MacBook Pro-mikrofon", 1, 0, 0.045, 0.01, IsDefaultInput: true, IsDefaultOutput: false);
    public static readonly AudioDeviceInfo UsbInterface = new(1, "USB Audio", 2, 2, 0.005, 0.005, IsDefaultInput: false, IsDefaultOutput: false);
    public static readonly AudioDeviceInfo Headphones = new(2, "Externa hörlurar", 0, 2, 0.01, 0.005, IsDefaultInput: false, IsDefaultOutput: true);
    public static readonly AudioDeviceInfo Speakers = new(3, "Högtalare i MacBook Pro", 0, 2, 0.01, 0.009, IsDefaultInput: false, IsDefaultOutput: false);

    public static readonly AudioDeviceInfo[] All = [Microphone, UsbInterface, Headphones, Speakers];
}
