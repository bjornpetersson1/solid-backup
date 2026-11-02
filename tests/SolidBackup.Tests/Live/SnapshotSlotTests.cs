using SolidBackup.Audio;

namespace SolidBackup.Tests.Live;

public class SnapshotSlotTests
{
    [Fact]
    public void NothingPublished_ReadsNothing()
    {
        Assert.False(new SnapshotSlot<Torn>().TryRead(out _));
    }

    [Fact]
    public void ReadsTheLatestValue()
    {
        SnapshotSlot<Torn> slot = new();
        slot.Publish(Torn.From(1));
        slot.Publish(Torn.From(2));

        Assert.True(slot.TryRead(out Torn value));
        Assert.Equal(Torn.From(2), value);
    }

    [Fact]
    public void AReaderOnAnotherThread_NeverSeesATornValue()
    {
        SnapshotSlot<Torn> slot = new();
        using CancellationTokenSource stop = new();
        long written = 0;
        Thread writer = new(() =>
        {
            for (long i = 1; !stop.IsCancellationRequested; i++)
            {
                slot.Publish(Torn.From(i));
                written = i;
            }
        });
        writer.Start();

        int reads = 0;
        long last = 0;
        DateTime until = DateTime.UtcNow.AddMilliseconds(300);
        while (DateTime.UtcNow < until)
        {
            if (slot.TryRead(out Torn value))
            {
                Assert.True(value.IsWhole, $"Torn read: {value}");
                Assert.True(value.A >= last, "A read went back in time.");
                last = value.A;
                reads++;
            }
        }

        stop.Cancel();
        writer.Join();

        Assert.True(reads > 1000, $"Only {reads} reads succeeded.");
        Assert.True(Volatile.Read(ref written) > 1000);
    }

    // Four fields that are only whole when they all come from the same write.
    private readonly record struct Torn(long A, long B, long C, long D)
    {
        public bool IsWhole => B == A * 3 && C == -A && D == (A ^ 0x5555);

        public static Torn From(long i)
        {
            return new Torn(i, i * 3, -i, i ^ 0x5555);
        }
    }
}
