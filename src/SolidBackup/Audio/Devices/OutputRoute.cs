namespace SolidBackup.Audio.Devices;

/// <summary>Where an output device plays: CoreAudio's transport type and current data source, as four-character codes.</summary>
public sealed record OutputRoute(string TransportType, string? DataSource)
{
    public const string BuiltInTransport = "bltn";
    public const string InternalSpeakersSource = "ispk";
    public const string HeadphonesSource = "hdpn";

    /// <summary>
    /// A built-in output that is not the headphone jack. Some Macs report no data source for
    /// their speakers, so only a known headphone source rules the speakers out.
    /// </summary>
    public bool IsBuiltInSpeakers => TransportType == BuiltInTransport && DataSource != HeadphonesSource;
}
