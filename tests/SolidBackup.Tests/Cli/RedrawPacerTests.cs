using SolidBackup.Cli;

namespace SolidBackup.Tests.Cli;

public class RedrawPacerTests
{
    [Fact]
    public void PolledEvery50Ms_RedrawsOncePerInterval()
    {
        TimeSpan now = TimeSpan.Zero;
        RedrawPacer pacer = new(LiveCommand.RedrawInterval, () => now);
        int redraws = 0;

        // A second of the stop keys' 50 ms polling.
        for (int poll = 1; poll <= 20; poll++)
        {
            now = TimeSpan.FromMilliseconds(poll * 50);
            redraws += pacer.Due() ? 1 : 0;
        }

        Assert.Equal(5, redraws);
    }

    [Fact]
    public void ASlowPoll_RedrawsOnceNotTwice()
    {
        TimeSpan now = TimeSpan.Zero;
        RedrawPacer pacer = new(TimeSpan.FromMilliseconds(200), () => now);

        now = TimeSpan.FromMilliseconds(450);

        Assert.True(pacer.Due());
        Assert.False(pacer.Due());
    }
}
