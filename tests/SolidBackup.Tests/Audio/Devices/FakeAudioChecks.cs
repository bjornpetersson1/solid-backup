using SolidBackup.Audio.Devices;

namespace SolidBackup.Tests.Audio.Devices;

internal sealed class FakePermission(MicrophonePermission status) : IMicrophonePermissionSource
{
    public MicrophonePermission Status() => status;
}

internal sealed class FakeRoutes(Func<string, OutputRoute?> route) : IOutputRouteSource
{
    public FakeRoutes(OutputRoute? route = null)
        : this(_ => route)
    {
    }

    public OutputRoute? Route(string deviceName) => route(deviceName);
}

internal static class TestPreflight
{
    public static readonly OutputRoute Speakers = new(OutputRoute.BuiltInTransport, OutputRoute.InternalSpeakersSource);
    public static readonly OutputRoute HeadphoneJack = new(OutputRoute.BuiltInTransport, "hdpn");

    public static AudioPreflight Create(
        MicrophonePermission permission = MicrophonePermission.Authorized, IOutputRouteSource? routes = null)
    {
        return new AudioPreflight(new FakePermission(permission), routes ?? new FakeRoutes(HeadphoneJack), "Terminal");
    }
}
