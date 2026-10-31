namespace SolidBackup.Audio.Devices;

/// <summary>macOS's microphone authorisation status; the values match AVAuthorizationStatus.</summary>
public enum MicrophonePermission
{
    NotDetermined = 0,
    Restricted = 1,
    Denied = 2,
    Authorized = 3,
    Unknown = -1,
}
