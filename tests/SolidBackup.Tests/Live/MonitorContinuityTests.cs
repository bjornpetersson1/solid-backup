using SolidBackup.Audio;
using SolidBackup.Live;
using SolidBackup.Tests.Analysis;
using SolidBackup.Tests.Audio;

namespace SolidBackup.Tests.Live;

/// <summary>
/// E8-1: the monitored input stays continuous when the input's and the output's callbacks arrive irregularly, in
/// bursts and on two clocks. A cut to silence or an uncrossfaded jump shows up as a sample-to-sample step far larger
/// than the signal's own.
/// </summary>
public class MonitorContinuityTests
{
    private const int SampleRate = TestSignal.SampleRate;
    private const int BufferSize = 64;
    private const int Seconds = 10;
    private const int OutputBuffers = Seconds * SampleRate / BufferSize;

    // A4 at −6 dBFS, below the soft limit's knee, so the monitor's output at 100 % is the input itself.
    private const int A4 = 69;
    private const double Amplitude = 0.501;

    // A crossfade between two parts of the same sine stays within this many times the sine's own largest step.
    private const double StepBound = 1.5;

    private static readonly int MaxDelay = (int)InputMonitor.MaxDelay(BufferSize, SampleRate);

    // One input sample per step of the ramp, so the output tells which input sample it plays.
    private const float RampStep = 1e-6f;

    public static TheoryData<double, int, bool> Schedules => new()
    {
        { 1.0, 1, false },
        { 1.0, 3, false },
        { 1.0, 3, true },
        { 1.001, 3, false },
        { 1.001, 3, true },
        { 0.999, 3, false },
        { 0.999, 3, true },
    };

    [Theory]
    [MemberData(nameof(Schedules))]
    public void ASine_StaysContinuous_ThroughIrregularCallbacks(double inputSpeed, int maxBurst, bool outputFirst)
    {
        InputMonitor monitor = Monitor();
        float[] sine = Sine(InputLength(inputSpeed));

        float[] output = Play(monitor, sine, CallbackSchedule.Irregular(OutputBuffers, inputSpeed, maxBurst, seed: 81, outputFirst));

        Assert.InRange(LargestStep(output), 0, StepBound * LargestStep(sine));
        Assert.Equal(0, monitor.Gaps);
        Assert.Equal(0, monitor.Overflows);
    }

    [Theory]
    [InlineData(1, false, 7)]
    [InlineData(3, false, 7)]
    [InlineData(3, true, 7)]
    [InlineData(3, false, 81)]
    [InlineData(3, true, 81)]
    public void BurstsWithoutDrift_NeedOnlyTheCorrectionThatSettlesTheStart(int maxBurst, bool outputFirst, int seed)
    {
        InputMonitor monitor = Monitor();
        List<int> corrected = [];

        Play(monitor, Sine(InputLength(1.0)), CallbackSchedule.Irregular(OutputBuffers, 1.0, maxBurst, seed, outputFirst), corrected);

        Assert.Equal(0, monitor.Gaps);
        Assert.InRange(monitor.Corrections, 0, 1);
        Assert.All(corrected, at => Assert.True(at < SampleRate, $"A correction at sample {at}, after the first second."));
    }

    [Theory]
    [MemberData(nameof(Schedules))]
    public void TheDelay_StaysWithinFiveMilliseconds(double inputSpeed, int maxBurst, bool outputFirst)
    {
        float[] ramp = [.. Enumerable.Range(0, InputLength(inputSpeed)).Select(i => i * RampStep)];
        List<int> corrected = [];

        float[] output = Play(Monitor(), ramp, CallbackSchedule.Irregular(OutputBuffers, inputSpeed, maxBurst, seed: 81, outputFirst), corrected);

        // Past the fade-in, and outside the crossfades: there the output mixes two input positions and has no single delay.
        int start = Array.FindIndex(output, sample => sample != 0) + (4 * BufferSize);
        int crossfade = Crossfade.Length(SampleRate);
        int next = 0;
        for (int i = start; i < output.Length; i++)
        {
            while (next < corrected.Count && corrected[next] + BufferSize + crossfade <= i)
            {
                next++;
            }

            if (next < corrected.Count && i >= corrected[next])
            {
                continue;
            }

            // When the played input sample was captured and when this output sample plays, on the output's clock: the
            // schedule starts one side half a buffer after the other.
            double captured = (output[i] / RampStep / inputSpeed) + (outputFirst ? BufferSize / 2 : 0);
            double plays = i + (outputFirst ? 0 : BufferSize / 2);
            Assert.True(plays - captured <= MaxDelay + 0.5, $"Output sample {i} is {plays - captured:0.0} samples behind its input.");
        }
    }

    [Fact]
    public void AnInputThatStops_FadesOutAndInAgain_CountingOneGap()
    {
        InputMonitor monitor = Monitor();
        float[] sine = Sine(3 * SampleRate);

        // Input for the first second, none for half a second, then input again.
        int second = SampleRate / BufferSize;
        IEnumerable<bool> steps = Enumerable.Range(0, 3 * second)
            .SelectMany(step => step < second || step >= 3 * second / 2 ? new[] { true, false } : [false]);
        float[] output = Play(monitor, sine, steps);

        Assert.InRange(LargestStep(output), 0, StepBound * LargestStep(sine));
        Assert.Equal(1, monitor.Gaps);
        Assert.Equal(0, output[(int)(1.25 * SampleRate)]);
        Assert.NotEqual(0, output[^1]);
    }

    [Fact]
    public void AStalledOutput_FadesOutAndInAgain_CountingOneOverflowPerStall()
    {
        InputMonitor monitor = Monitor();
        float[] sine = Sine(4 * SampleRate);

        // Steady, except that twice the output stalls for 0.1 s while the input goes on.
        int second = SampleRate / BufferSize;
        int stall = second / 10;
        IEnumerable<bool> steps = Enumerable.Range(0, 3 * second).SelectMany(step =>
            step is 50 or 120 ? Enumerable.Repeat(true, stall + 1).Append(false) : new[] { true, false });
        float[] output = Play(monitor, sine, steps);

        Assert.InRange(LargestStep(output), 0, StepBound * LargestStep(sine));
        Assert.Equal(2, monitor.Overflows);
        Assert.Equal(2, monitor.Interruptions);
    }

    private static InputMonitor Monitor()
    {
        return new InputMonitor(BufferSize, levelPercent: 100, SampleRate);
    }

    // Enough input for the output buffers on the input's clock, with a few buffers to spare.
    private static int InputLength(double inputSpeed)
    {
        return (int)(OutputBuffers * inputSpeed * BufferSize) + (8 * BufferSize);
    }

    private static float[] Sine(int length)
    {
        return TestSignal.Sine(A4, Amplitude, (double)length / SampleRate);
    }

    private static double LargestStep(float[] samples)
    {
        return TestSignal.LargestStep(samples, 0, samples.Length);
    }

    // Runs the monitor's two sides in the given order (true for an input buffer) over silent chords; returns what the
    // output played. With corrected, also notes where each buffer that started a correction or a gap begins.
    private static float[] Play(InputMonitor monitor, float[] input, IEnumerable<bool> steps, List<int>? corrected = null)
    {
        List<float> output = [];
        float[] buffer = new float[BufferSize];
        int read = 0;
        foreach (bool step in steps)
        {
            if (step)
            {
                if (read + BufferSize <= input.Length)
                {
                    monitor.Write(input.AsSpan(read, BufferSize));
                    read += BufferSize;
                }

                continue;
            }

            Array.Clear(buffer);
            long changes = monitor.Corrections + monitor.Gaps;
            monitor.MixInto(buffer);
            if (monitor.Corrections + monitor.Gaps != changes)
            {
                corrected?.Add(output.Count);
            }

            output.AddRange(buffer);
        }

        return [.. output];
    }
}
