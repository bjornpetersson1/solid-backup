using SolidBackup.Audio;

namespace SolidBackup.Tests.Audio;

public class HandOverBufferTests
{
    private const int SampleRate = 48_000;
    private const int BufferSize = 64;

    // Reads in one drift window: 0.5 s of buffers.
    private const int WindowReads = SampleRate / 2 / BufferSize;

    private readonly HandOverBuffer _handOver = new(SampleRate, BufferSize);
    private readonly float[] _read = new float[BufferSize];

    [Fact]
    public void Read_ReturnsBuffersInWrittenOrder()
    {
        _handOver.Write(Filled(1));
        _handOver.Write(Filled(2));

        _handOver.Read(_read);
        Assert.All(_read, sample => Assert.Equal(1f, sample));
        _handOver.Read(_read);
        Assert.All(_read, sample => Assert.Equal(2f, sample));
        Assert.Equal(0, _handOver.Underruns);
    }

    [Fact]
    public void Read_WhenEmpty_PlaysSilenceAndCountsUnderrun()
    {
        _handOver.Write(Filled(1));
        _handOver.Read(_read);
        Array.Fill(_read, 9f);

        _handOver.Read(_read);

        Assert.All(_read, sample => Assert.Equal(0f, sample));
        Assert.Equal(1, _handOver.Underruns);
    }

    [Fact]
    public void Read_BeforeTheFirstInput_PlaysSilenceWithoutCountingUnderruns()
    {
        Array.Fill(_read, 9f);

        _handOver.Read(_read);
        _handOver.Read(_read);

        Assert.All(_read, sample => Assert.Equal(0f, sample));
        Assert.Equal(0, _handOver.Underruns);
    }

    [Fact]
    public void Write_WrapsAroundTheRing()
    {
        for (int i = 0; i < 2000; i++)
        {
            _handOver.Write(Ramp(i));
            _handOver.Read(_read);
            Assert.Equal(Ramp(i), _read);
        }

        Assert.Equal(0, _handOver.Underruns);
        Assert.Equal(0, _handOver.Overruns);
    }

    [Fact]
    public void RepeatedBursts_AreNotDropped()
    {
        // The input delivers four buffers at once and then nothing for three reads, as CoreAudio does.
        for (int read = 0; read < WindowReads * 3; read++)
        {
            if (read % 4 == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    _handOver.Write(Filled(read + i));
                }
            }

            _handOver.Read(_read);
            Assert.All(_read, sample => Assert.Equal(read, sample));
        }

        Assert.Equal(0, _handOver.DriftCorrections);
        Assert.Equal(0, _handOver.Underruns);
    }

    [Fact]
    public void PersistentBacklog_IsDroppedToOneBuffer()
    {
        _handOver.Write(Filled(-1));
        _handOver.Write(Filled(-1));
        _handOver.Write(Filled(-1));
        for (int i = 0; i < WindowReads; i++)
        {
            _handOver.Write(Filled(i));
            _handOver.Read(_read);
        }

        Assert.Equal(1, _handOver.DriftCorrections);
        _handOver.Write(Filled(1000));
        _handOver.Read(_read);
        Assert.All(_read, sample => Assert.Equal(1000f, sample));
        Assert.Equal(0, _handOver.Underruns);
    }

    [Fact]
    public void Write_WhenFull_DropsInputAndCountsOverrun()
    {
        int capacityBuffers = 32_768 / BufferSize;
        for (int i = 0; i <= capacityBuffers; i++)
        {
            _handOver.Write(Filled(i));
        }

        Assert.Equal(1, _handOver.Overruns);
    }

    [Fact]
    public void Reset_EmptiesTheRingAndUnprimesIt()
    {
        _handOver.Write(Filled(1));
        _handOver.Write(Filled(2));
        _handOver.Read(_read);

        _handOver.Reset();
        _handOver.Read(_read);

        Assert.All(_read, sample => Assert.Equal(0f, sample));
        Assert.Equal(0, _handOver.Underruns);
        _handOver.Write(Filled(3));
        _handOver.Read(_read);
        Assert.All(_read, sample => Assert.Equal(3f, sample));
    }

    [Fact]
    public void WriteAndRead_AllocateNothing()
    {
        float[] buffer = Filled(1);
        for (int i = 0; i < 100; i++)
        {
            _handOver.Write(buffer);
            _handOver.Read(_read);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        {
            _handOver.Write(buffer);
            _handOver.Read(_read);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static float[] Filled(float value)
    {
        float[] buffer = new float[BufferSize];
        Array.Fill(buffer, value);
        return buffer;
    }

    private static float[] Ramp(int start)
    {
        return Enumerable.Range(start * BufferSize, BufferSize).Select(i => (float)i).ToArray();
    }
}
