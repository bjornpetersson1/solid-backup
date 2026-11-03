using SolidBackup.Audio.Devices;
using SolidBackup.Cli;
using SolidBackup.Live;
using SolidBackup.Tests.Audio.Devices;

namespace SolidBackup.Tests.Cli;

public class MonitorGuardTests
{
    private static readonly SolidBackup.Configuration.AppSettings Monitored = TestSettings.Default with { MonitorOn = true };

    [Fact]
    public void AnOutputThatBecomesUnsafe_StopsTheMonitoringOnTheNextCheck()
    {
        TimeSpan now = TimeSpan.Zero;
        bool safe = true;
        LiveMode mode = new(new FakeDevices(), Monitored, chainDelayMs: null, outputSafe: true);
        MonitorGuard guard = new(mode, () => safe, new RedrawPacer(LiveCommand.MonitorCheckInterval, () => now));

        now += LiveCommand.MonitorCheckInterval;
        guard.Check();
        Assert.True(mode.Monitoring);

        safe = false;
        now += LiveCommand.MonitorCheckInterval / 2;
        guard.Check();
        Assert.True(mode.Monitoring);
        now += LiveCommand.MonitorCheckInterval;
        guard.Check();
        Assert.False(mode.Monitoring);

        safe = true;
        now += LiveCommand.MonitorCheckInterval;
        guard.Check();
        Assert.False(mode.Monitoring);
    }

    [Theory]
    [InlineData("headphones", true)]
    [InlineData("speakers", false)]
    [InlineData("unknown", false)]
    public void TheOutput_IsSafeForMonitoringOnlyWhenItsRouteIsKnownNotToBeTheSpeakers(string route, bool safe)
    {
        OutputRoute? read = route switch
        {
            "headphones" => TestPreflight.HeadphoneJack,
            "speakers" => TestPreflight.Speakers,
            _ => null,
        };

        Assert.Equal(safe, TestPreflight.Create(routes: new FakeRoutes(read)).IsSafeForMonitoring("Inbyggd utgång"));
    }
}
