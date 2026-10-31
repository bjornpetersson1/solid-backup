namespace SolidBackup.Audio.Devices;

/// <summary>Chooses a device by name: exact, ignoring case and Unicode form; an empty name means the system default.</summary>
public static class DeviceSelector
{
    public static AudioDeviceInfo? Find(IReadOnlyList<AudioDeviceInfo> devices, string name, AudioDirection direction)
    {
        return devices.FirstOrDefault(device => device.Supports(direction) && Matches(device, name, direction));
    }

    public static AudioDeviceInfo Select(IReadOnlyList<AudioDeviceInfo> devices, string name, AudioDirection direction)
    {
        return Find(devices, name, direction) ?? throw new AudioDeviceException(MissingMessage(devices, name, direction));
    }

    /// <summary>Says that no device matches <paramref name="name"/> and lists those that exist.</summary>
    public static string MissingMessage(IReadOnlyList<AudioDeviceInfo> devices, string name, AudioDirection direction)
    {
        string kind = direction == AudioDirection.Input ? "ljudingång" : "ljudutgång";
        string[] available = devices.Where(device => device.Supports(direction)).Select(device => device.Name).ToArray();
        string missing = name.Length == 0 ? $"Det finns ingen {kind} som är systemets standard." : $"Det finns ingen {kind} som heter \"{name}\".";
        string list = available.Length == 0 ? "Det finns inga alls." : $"Tillgängliga: {string.Join(", ", available)}.";
        return $"{missing} {list}";
    }

    private static bool Matches(AudioDeviceInfo device, string name, AudioDirection direction)
    {
        // macOS may report a name decomposed ("o" + combining diaeresis) while the terminal sends "ö".
        return name.Length == 0
            ? device.IsDefault(direction)
            : string.Equals(device.Name.Normalize(), name.Normalize(), StringComparison.OrdinalIgnoreCase);
    }
}
