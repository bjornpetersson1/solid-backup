using SolidBackup.Analysis;

namespace SolidBackup.Tests.Analysis;

public sealed class OnsetDetectorTests
{
    private const int Buffer = 64;

    [Fact]
    public void NoteFromSilence_GivesOneOnsetWithin20MsOfItsStart()
    {
        float[] input = [.. TestSignal.Silence(0.5), .. TestSignal.Sine(57, 0.5, 0.5)];

        double[] onsets = Onsets(input);

        Assert.Single(onsets);
        Assert.InRange(onsets[0], 500 - 20, 500 + 20);
    }

    [Fact]
    public void SteadyNote_GivesNoOnsetAfterItsStart()
    {
        double[] onsets = Onsets([.. TestSignal.Silence(0.1), .. TestSignal.Sine(57, 0.5, 2)]);

        Assert.Single(onsets);
        Assert.InRange(onsets[0], 80, 120);
    }

    [Fact]
    public void NotesFasterThanTheMinimumInterval_GiveOneOnsetPerInterval()
    {
        // Notes every 40 ms (20 ms on, 20 ms off) for one second, with a minimum interval of 100 ms.
        float[] input = [.. TestSignal.Silence(0.1)];
        for (int i = 0; i < 25; i++)
        {
            input = [.. input, .. TestSignal.Sine(57, 0.5, 0.02), .. TestSignal.Silence(0.02)];
        }

        double[] onsets = Onsets(input);

        Assert.InRange(onsets.Length, 9, 10);
        Assert.All(onsets.Zip(onsets.Skip(1)), pair => Assert.True(pair.Second - pair.First >= 100));
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(2048)]
    public void BuffersLongerThanTheRiseTime_StillFindTheOnset(int bufferLength)
    {
        float[] input = [.. TestSignal.Silence(0.5), .. TestSignal.Sine(57, 0.5, 0.5)];
        LevelMeter meter = new(TestSignal.SampleRate);
        OnsetDetector detector = new(TestSignal.SampleRate, 15, 100, meter.WindowLength);

        double[] onsets = TestSignal.InBuffers(input, bufferLength, (all, offset) => detector.Process(meter.Process(all.AsSpan(offset, bufferLength)), bufferLength))
            .OfType<double>()
            .ToArray();

        // The onset is placed in the middle of the buffer that holds the attack.
        Assert.Single(onsets);
        Assert.InRange(onsets[0], 500 - (bufferLength * 1000.0 / TestSignal.SampleRate), 500 + (bufferLength * 1000.0 / TestSignal.SampleRate));
    }

    [Fact]
    public void QuietRiseBelowTheThreshold_GivesNoOnset()
    {
        // From −40 dBFS to −30 dBFS: a 10 dB rise, below the 15 dB threshold.
        Assert.Empty(Onsets([.. TestSignal.Sine(57, 0.01414, 0.3), .. TestSignal.Sine(57, 0.04472, 0.3)]));
    }

    [Fact]
    public void SameInputTwice_GivesIdenticalOnsets()
    {
        float[] input = [.. TestSignal.Silence(0.2), .. TestSignal.Sine(57, 0.5, 0.3), .. TestSignal.Silence(0.2), .. TestSignal.Sine(60, 0.3, 0.3)];

        Assert.Equal(Onsets(input), Onsets(input));
    }

    [Fact]
    public void PerBufferCall_AllocatesNothing()
    {
        OnsetDetector detector = new(TestSignal.SampleRate, 15, 100, levelWindowLength: 480);
        detector.Process(-60, Buffer);

        // Levels that keep jumping, so onsets fire and the ring of recent levels turns over.
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 2000; i++)
        {
            detector.Process(i % 50 < 25 ? -60 : -20, Buffer);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static double[] Onsets(float[] samples)
    {
        LevelMeter meter = new(TestSignal.SampleRate);
        OnsetDetector detector = new(TestSignal.SampleRate, thresholdDb: 15, minIntervalMs: 100, meter.WindowLength);
        return TestSignal.InBuffers(samples, Buffer, (all, offset) => detector.Process(meter.Process(all.AsSpan(offset, Buffer)), Buffer))
            .OfType<double>()
            .ToArray();
    }
}
