using SolidBackup.Audio;
using SolidBackup.Tests.Analysis;

namespace SolidBackup.Tests.Audio;

/// <summary>E8-3: the player's callback pauses, resumes and seeks without a click, and allocates nothing.</summary>
public class PlaybackCursorTests
{
    private const int Buffer = 64;

    // A crossfade between two parts of the same sine, or to and from silence, stays within this of the sine's own step.
    private const double StepBound = 1.5;

    private static readonly float[] Sine = TestSignal.Sine(69, 0.5, seconds: 1);

    [Fact]
    public void PausingResumingAndSeeking_NeverStepBeyondTheSignalsOwn()
    {
        PlaybackCursor cursor = new();
        List<float> played = [];
        for (int buffer = 0; buffer < 100; buffer++)
        {
            if (buffer == 10)
            {
                cursor.Paused = true;
            }

            if (buffer == 20)
            {
                cursor.Paused = false;
            }

            if (buffer is 30 or 31)
            {
                cursor.Seek(cursor.Position + 1000, Sine.Length);
            }

            if (buffer == 50)
            {
                cursor.Seek(cursor.Position - 3000, Sine.Length);
                cursor.Paused = true;
            }

            if (buffer == 60)
            {
                cursor.Paused = false;
            }

            played.AddRange(Next(cursor));
        }

        float[] output = [.. played];
        Assert.InRange(TestSignal.LargestStep(output, 0, output.Length), 0, StepBound * TestSignal.LargestStep(Sine, 0, Sine.Length));
        Assert.All(output.AsSpan(11 * Buffer, 9 * Buffer).ToArray(), sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void WhilePaused_ThePositionStays_AndASeekIsTakenUp()
    {
        PlaybackCursor cursor = new();
        Next(cursor);
        cursor.Paused = true;
        Next(cursor);
        int pausedAt = cursor.Position;
        Next(cursor);
        cursor.Seek(5000, Sine.Length);
        Next(cursor);

        Assert.Equal(2 * Buffer, pausedAt);
        Assert.Equal(5000, cursor.Position);
    }

    [Fact]
    public void SeekingForwardPastTheEnd_WhileTheSilenceAfterItPlays_DoesNothing()
    {
        PlaybackCursor cursor = new();
        cursor.Seek(Sine.Length, Sine.Length);
        Next(cursor);
        Next(cursor);
        int position = cursor.Position;

        cursor.Seek(position + 1000, Sine.Length);
        Next(cursor);

        Assert.Equal(position + Buffer, cursor.Position);
    }

    [Fact]
    public void Fill_AllocatesNothing()
    {
        PlaybackCursor cursor = new();
        float[] interleaved = new float[2 * Buffer];
        cursor.Fill(Sine, interleaved, channels: 2);

        long before = GC.GetAllocatedBytesForCurrentThread();
        cursor.Paused = true;
        cursor.Fill(Sine, interleaved, channels: 2);
        cursor.Seek(1000, Sine.Length);
        cursor.Paused = false;
        cursor.Fill(Sine, interleaved, channels: 2);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(interleaved[^2], interleaved[^1]);
    }

    private static float[] Next(PlaybackCursor cursor)
    {
        float[] buffer = new float[Buffer];
        cursor.Fill(Sine, buffer, channels: 1);
        return buffer;
    }
}
