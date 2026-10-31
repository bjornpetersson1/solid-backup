namespace SolidBackup.Audio;

/// <summary>The audio system or a device could not be opened; the message is shown to the user.</summary>
public sealed class AudioDeviceException(string message, Exception? innerException = null)
    : Exception(message, innerException);
