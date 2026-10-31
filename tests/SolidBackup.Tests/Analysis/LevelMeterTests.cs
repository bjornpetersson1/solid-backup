using SolidBackup.Analysis;

namespace SolidBackup.Tests.Analysis;

public sealed class LevelMeterTests
{
    private const int Buffer = 64;

    [Theory]
    [InlineData(1.0, -3.0)]
    [InlineData(0.1, -23.0)]
    public void Sine_ReadsItsRmsLevel(double amplitude, double expectedDbfs)
    {
        double level = Levels(TestSignal.Sine(57, amplitude))[^1];

        Assert.Equal(expectedDbfs, level, 0.1);
    }

    [Fact]
    public void Silence_ReadsTheFloor()
    {
        Assert.All(Levels(TestSignal.Silence(0.1)), level => Assert.Equal(LevelMeter.FloorDbfs, level));
    }

    [Fact]
    public void FirstBuffer_ReadsTheInputsOwnLevel()
    {
        LevelMeter meter = new(TestSignal.SampleRate);

        // Half a window of A6: enough whole periods for its RMS to read −3 dBFS.
        Assert.Equal(-3.0, meter.Process(TestSignal.Sine(93, 1).AsSpan(0, 480 / 2)), 0.2);
    }

    [Fact]
    public void Level_FollowsTheLast10Ms()
    {
        float[] input = [.. TestSignal.Sine(57, 1, 0.1), .. TestSignal.Silence(0.02)];

        Assert.Equal(LevelMeter.FloorDbfs, Levels(input)[^1]);
    }

    [Fact]
    public void SameInputTwice_GivesIdenticalLevels()
    {
        float[] input = [.. TestSignal.Sine(50, 0.3, 0.2), .. TestSignal.Silence(0.05), .. TestSignal.Sine(60, 0.05, 0.2)];

        Assert.Equal(Levels(input), Levels(input));
    }

    [Fact]
    public void PerBufferCall_AllocatesNothing()
    {
        LevelMeter meter = new(TestSignal.SampleRate);
        float[] tone = TestSignal.Sine(57, 0.5);
        meter.Process(tone.AsSpan(0, Buffer));

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int offset = Buffer; offset + Buffer <= tone.Length; offset += Buffer)
        {
            meter.Process(tone.AsSpan(offset, Buffer));
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static double[] Levels(float[] samples)
    {
        LevelMeter meter = new(TestSignal.SampleRate);
        return TestSignal.InBuffers(samples, Buffer, (all, offset) => meter.Process(all.AsSpan(offset, Buffer)));
    }
}
