using SolidBackup.Audio;

namespace SolidBackup.Tests.Audio;

public class SplicingRingTests
{
    [Fact]
    public void ADrop_IsLimitedToWhatWaitsBeyondOneCrossfade()
    {
        SplicingRing ring = new(least: 64, crossfadeLength: 4);
        ring.Write([.. Enumerable.Range(0, 10).Select(i => (float)i)]);

        Assert.True(ring.Splice(100, ring.Written));

        Assert.Equal(4, ring.Waiting(ring.Written));
    }

    [Fact]
    public void ADrop_WithNoMoreThanACrossfadeWaiting_DoesNotMove()
    {
        SplicingRing ring = new(least: 64, crossfadeLength: 4);
        ring.Write([1, 2, 3, 4]);

        Assert.False(ring.Splice(2, ring.Written));
        Assert.Equal(4, ring.Waiting(ring.Written));
    }

    [Fact]
    public void ARepeat_CrossfadesOutOfWhatWasPlayed_Backwards()
    {
        SplicingRing ring = new(least: 64, crossfadeLength: 2);
        ring.Write([10, 20, 30, 40]);
        for (int i = 0; i < 4; i++)
        {
            ring.Played(ring.Next());
        }

        Assert.True(ring.Splice(-2, ring.Written));

        // From 40, 30 (backwards) into 30, 40 (repeated), weights 1/3 and 2/3.
        Assert.Equal(40 + ((30 - 40) / 3f), ring.Next(), 1e-5f);
        Assert.Equal(30 + (2 * (40 - 30) / 3f), ring.Next(), 1e-5f);
    }
}
