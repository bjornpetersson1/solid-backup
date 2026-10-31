namespace SolidBackup.Audio.Devices;

/// <summary>The outcome of the microphone check: whether audio must not start, and what to tell the user.</summary>
public sealed record MicrophoneCheck(bool Blocked, string? Message)
{
    public static readonly MicrophoneCheck Clear = new(false, null);
}
