using SolidBackup.Analysis;
using SolidBackup.Measurement;

namespace SolidBackup.Tests.Analysis;

public sealed class PitchDetectorTests
{
    private const int SampleRate = 48_000;
    private const int Buffer = 64;
    private const int E2 = 40;
    private const int C7 = 96;

    [Theory]
    [InlineData(28)]
    [InlineData(35)]
    [InlineData(103)]
    [InlineData(108)]
    public void ToneWellOutsideTheRange_GivesNoNote(int midi)
    {
        Assert.DoesNotContain(Steady(new PitchDetector(SampleRate, E2, C7, 0.8), Sine(midi, 0.5)), estimate => estimate.HasNote);
    }

    [Theory]
    [InlineData(E2)]
    [InlineData(C7)]
    public void ToneAtEitherEndOfTheRange_IsFound(int midi)
    {
        Assert.All(Steady(new PitchDetector(SampleRate, E2, C7, 0.8), Sine(midi, 0.5)), estimate => Assert.Equal(midi, estimate.MidiNote));
    }

    [Theory]
    [InlineData(45, false)]
    [InlineData(57, true)]
    [InlineData(69, true)]
    [InlineData(81, false)]
    public void NarrowedRange_ReportsOnlyNotesInside(int midi, bool expected)
    {
        PitchDetector detector = new(SampleRate, lowestMidi: 57, highestMidi: 69, 0.8);

        Assert.All(Steady(detector, Sine(midi, 0.5)), estimate => Assert.Equal(expected, estimate.HasNote));
    }

    [Fact]
    public void ConfidenceBelowTheThreshold_GivesNoNote()
    {
        float[] noisy = Mix(Sine(57, 0.3), Noise(0.3));

        Assert.All(Steady(new PitchDetector(SampleRate, E2, C7, 0.5), noisy), estimate => Assert.True(estimate.HasNote));
        Assert.All(Steady(new PitchDetector(SampleRate, E2, C7, 0.99), noisy), estimate => Assert.False(estimate.HasNote));
    }

    [Fact]
    public void DigitalSilenceAndToneBelowTheEnergyFloor_GiveNoNote()
    {
        Assert.All(Run(new PitchDetector(SampleRate, E2, C7, 0.8), new float[SampleRate]), estimate => Assert.False(estimate.HasNote));
        Assert.All(Run(new PitchDetector(SampleRate, E2, C7, 0.8), Sine(57, 0.00003)), estimate => Assert.False(estimate.HasNote));
    }

    [Fact]
    public void NoteChange_IsReportedWithinHalfAWindowPlusTheHold()
    {
        PitchDetector detector = new(SampleRate, E2, C7, 0.8);
        float[] change = [.. Sine(57, 0.5), .. Sine(60, 0.5)];
        double changeMs = 1000;

        PitchEstimate first = Run(detector, change).First(estimate => estimate.TimeMs > changeMs - 100 && estimate.MidiNote == 60);

        double halfWindowMs = detector.WindowLength / 2.0 * 1000 / SampleRate;
        double delayMs = first.TimeMs + halfWindowMs - changeMs;
        Assert.InRange(delayMs, 0, halfWindowMs + NoteHold.HoldMs + (2.0 * Buffer * 1000 / SampleRate));
    }

    [Fact]
    public void TimeOfAnEstimate_IsTheCentreOfItsWindow()
    {
        PitchDetector detector = new(SampleRate, E2, C7, 0.8);

        PitchEstimate[] estimates = Run(detector, new float[Buffer * 100]);

        Assert.Equal(((Buffer * 100) - (detector.WindowLength / 2.0)) * 1000 / SampleRate, estimates[^1].TimeMs, 1e-9);
    }

    [Fact]
    public void SameInputTwice_GivesIdenticalEstimates()
    {
        float[] input = [.. Mix(Sine(52, 0.4), Noise(0.05)), .. Sine(64, 0.4)];

        Assert.Equal(Run(new PitchDetector(SampleRate, E2, C7, 0.8), input), Run(new PitchDetector(SampleRate, E2, C7, 0.8), input));
    }

    [Fact]
    public void PerBufferCall_AllocatesNothing()
    {
        PitchDetector detector = new(SampleRate, E2, C7, 0.8);
        float[] tone = Sine(57, 0.5);
        for (int offset = 0; offset < SampleRate / 2; offset += Buffer)
        {
            detector.Process(tone.AsSpan(offset, Buffer));
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int offset = SampleRate / 2; offset + Buffer <= tone.Length; offset += Buffer)
        {
            detector.Process(tone.AsSpan(offset, Buffer));
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void BufferLongerThanTheWindow_IsHandled()
    {
        PitchDetector detector = new(SampleRate, E2, C7, 0.8);

        float[] tone = Sine(57, 0.5);

        // The first estimate is held back: a new note needs two estimates.
        detector.Process(tone.AsSpan(0, 4096));
        PitchEstimate estimate = detector.Process(tone.AsSpan(4096, 4096));

        Assert.Equal(57, estimate.MidiNote);
    }

    private static PitchEstimate[] Run(PitchDetector detector, float[] samples)
    {
        List<PitchEstimate> estimates = [];
        for (int offset = 0; offset + Buffer <= samples.Length; offset += Buffer)
        {
            estimates.Add(detector.Process(samples.AsSpan(offset, Buffer)));
        }

        return [.. estimates];
    }

    // The estimates once the window is full and the hold has passed.
    private static PitchEstimate[] Steady(PitchDetector detector, float[] samples)
    {
        return Run(detector, samples).Skip(SampleRate / 10 / Buffer).ToArray();
    }

    private static float[] Sine(int midi, double amplitude)
    {
        double frequency = Pitch.Frequency(midi * 100.0);
        return Enumerable.Range(0, SampleRate).Select(i => (float)(amplitude * Math.Sin(2 * Math.PI * frequency * i / SampleRate))).ToArray();
    }

    private static float[] Noise(double amplitude)
    {
        Random random = new(7);
        return Enumerable.Range(0, SampleRate).Select(_ => (float)(amplitude * ((random.NextDouble() * 2) - 1))).ToArray();
    }

    private static float[] Mix(float[] first, float[] second)
    {
        return first.Zip(second, (a, b) => a + b).ToArray();
    }
}
