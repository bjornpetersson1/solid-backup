using SolidBackup.Analysis;

namespace SolidBackup.Tests.Analysis;

public sealed class NoiseGateTests
{
    private const int Buffer = 48;

    // Threshold −45 dBFS, closing below −48 dBFS after 10 ms (10 buffers of 1 ms).
    private static NoiseGate Gate(double thresholdDbfs = -45)
    {
        return new NoiseGate(TestSignal.SampleRate, thresholdDbfs, hysteresisDb: 3, holdMs: 10);
    }

    [Fact]
    public void LevelReachingTheThreshold_OpensTheGate()
    {
        NoiseGate gate = Gate();

        Assert.False(gate.Process(-45.1, Buffer));
        Assert.True(gate.Process(-45, Buffer));
    }

    [Fact]
    public void LevelHoveringAroundTheThreshold_DoesNotChatter()
    {
        NoiseGate gate = Gate();
        gate.Process(-40, Buffer);

        bool[] states = Enumerable.Range(0, 200).Select(i => gate.Process(i % 2 == 0 ? -44 : -47, Buffer)).ToArray();

        Assert.All(states, Assert.True);
    }

    [Fact]
    public void DropShorterThanTheHold_KeepsTheGateOpen()
    {
        NoiseGate gate = Gate();
        gate.Process(-40, Buffer);

        bool[] during = Enumerable.Range(0, 9).Select(_ => gate.Process(-60, Buffer)).ToArray();
        bool after = gate.Process(-40, Buffer);

        Assert.All(during, Assert.True);
        Assert.True(after);
    }

    [Fact]
    public void DropLastingTheHold_ClosesTheGate()
    {
        NoiseGate gate = Gate();
        gate.Process(-40, Buffer);

        // The hold runs from the first low reading: 10 ms later is the eleventh.
        bool[] states = Enumerable.Range(0, 11).Select(_ => gate.Process(-60, Buffer)).ToArray();

        Assert.All(states[..^1], Assert.True);
        Assert.False(states[^1]);
    }

    [Fact]
    public void LevelAboveTheCloseThreshold_NeverClosesTheGate()
    {
        NoiseGate gate = Gate();
        gate.Process(-40, Buffer);

        Assert.All(Enumerable.Range(0, 100).Select(_ => gate.Process(-47.9, Buffer)), Assert.True);
    }

    [Fact]
    public void SingleLowReading_NeverClosesTheGate_EvenWithBuffersLongerThanTheHold()
    {
        NoiseGate gate = Gate();
        gate.Process(-40, 1024);

        Assert.True(gate.Process(-60, 1024));
        Assert.False(gate.Process(-60, 1024));
    }

    [Fact]
    public void SameLevelsTwice_GiveIdenticalStates()
    {
        double[] levels = Enumerable.Range(0, 500).Select(i => -70 + (35 * Math.Abs(Math.Sin(i * 0.05)))).ToArray();

        Assert.Equal(States(levels), States(levels));
    }

    [Fact]
    public void PerBufferCall_AllocatesNothing()
    {
        NoiseGate gate = Gate();
        gate.Process(-40, Buffer);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 2000; i++)
        {
            gate.Process(i % 40 < 20 ? -30 : -70, Buffer);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData(-45, true)]
    [InlineData(-35, false)]
    public void ChangedThreshold_MovesWhereTheGateOpens(double thresholdDbfs, bool opensAtMinus40)
    {
        Assert.Equal(opensAtMinus40, Gate(thresholdDbfs).Process(-40, Buffer));
    }

    private static bool[] States(double[] levels)
    {
        NoiseGate gate = Gate();
        return levels.Select(level => gate.Process(level, Buffer)).ToArray();
    }
}
