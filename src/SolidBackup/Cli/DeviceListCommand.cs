using SolidBackup.Audio;
using SolidBackup.Audio.Devices;
using SolidBackup.Configuration;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>Lists the audio devices and marks the defaults, the chosen ones and the built-in speakers.</summary>
public sealed class DeviceListCommand(
    IAnsiConsole console, Func<SettingsLoadResult> loadSettings, Func<IAudioDeviceCatalog> openCatalog, AudioPreflight preflight)
{
    /// <summary>How a system default device is marked, here and in the menu.</summary>
    public const string DefaultTag = "[standard]";

    public int Run()
    {
        SettingsLoadResult loaded = loadSettings();
        ConsoleMessages.SettingsMessage(console, loaded);

        try
        {
            using IAudioDeviceCatalog catalog = openCatalog();
            WriteDevices(catalog.Devices, AudioDirection.Input, loaded.Settings.InputDevice);
            WriteDevices(catalog.Devices, AudioDirection.Output, loaded.Settings.OutputDevice);
            return 0;
        }
        catch (AudioDeviceException exception)
        {
            return ConsoleMessages.AudioError(console, exception);
        }
    }

    private static string Describe(AudioDeviceInfo device, AudioDirection direction, bool chosen, bool builtInSpeakers)
    {
        int channels = device.Channels(direction);
        string[] tags =
        [
            $"{device.Name} ({channels} {(channels == 1 ? "kanal" : "kanaler")})",
            device.IsDefault(direction) ? DefaultTag : "",
            chosen ? "[vald]" : "",
            builtInSpeakers ? "[inbyggda högtalare]" : "",
        ];
        return string.Join(" ", tags.Where(tag => tag.Length > 0));
    }

    private void WriteDevices(IReadOnlyList<AudioDeviceInfo> devices, AudioDirection direction, string chosenName)
    {
        console.WriteLine(direction == AudioDirection.Input ? "Ingångar:" : "Utgångar:");
        AudioDeviceInfo? chosen = DeviceSelector.Find(devices, chosenName, direction);
        foreach (AudioDeviceInfo device in devices.Where(device => device.Supports(direction)))
        {
            bool builtInSpeakers = direction == AudioDirection.Output && preflight.IsBuiltInSpeakers(device.Name);
            console.WriteLine($"  {Describe(device, direction, device == chosen, builtInSpeakers)}");
        }

        if (chosen is null)
        {
            ConsoleMessages.Warning(console, $"  {DeviceSelector.MissingMessage(devices, chosenName, direction)}");
        }
    }
}
