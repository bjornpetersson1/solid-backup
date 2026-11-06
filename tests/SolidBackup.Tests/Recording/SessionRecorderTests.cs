using System.Buffers.Binary;

using SolidBackup.Recording;
using SolidBackup.Tests.Audio;

namespace SolidBackup.Tests.Recording;

public class SessionRecorderTests
{
    private const int SampleRate = 48_000;
    private const int BufferSize = 64;

    [Fact]
    public void Recording_PutsTheInputLeftAndTheOutputRightInSync()
    {
        KeptStream stream = new();
        SessionRecorder recorder = new(stream, SampleRate, BufferSize);
        float[] input = Ramp(0.001f);
        float[] output = Ramp(-0.002f);
        for (int i = 0; i < 100; i++)
        {
            recorder.OnBuffer(input, output);
        }

        recorder.Stop();

        byte[] file = stream.Contents;
        Assert.Equal(100 * BufferSize * 4, (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(40)));
        for (int frame = 0; frame < BufferSize * 3; frame++)
        {
            Assert.Equal(input[frame % BufferSize], TestWav.Pcm16Sample(file, frame, 0, channels: 2), precision: 4);
            Assert.Equal(output[frame % BufferSize], TestWav.Pcm16Sample(file, frame, 1, channels: 2), precision: 4);
        }

        Assert.Equal(100.0 * BufferSize / SampleRate, recorder.Seconds, precision: 6);
        Assert.Equal(0, recorder.Overruns);
    }

    [Fact]
    public void InputAndOutputFromTwoThreads_StayInStep()
    {
        KeptStream stream = new();
        SessionRecorder recorder = new(stream, SampleRate, BufferSize);
        const int Buffers = 2000;

        // The two threads keep pace buffer by buffer, as two audio callbacks on clocks that agree.
        using Barrier pace = new(2);
        Thread inputThread = new(() => Feed(recorder.OnInput, Buffers, Numbered, pace));
        Thread outputThread = new(() => Feed(recorder.OnOutput, Buffers, Numbered, pace));
        inputThread.Start();
        outputThread.Start();
        inputThread.Join();
        outputThread.Join();
        recorder.Stop();

        byte[] file = stream.Contents;
        Assert.Equal(Buffers * BufferSize * 4, (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(40)));
        for (int buffer = 0; buffer < Buffers; buffer += 7)
        {
            Assert.Equal(Numbered(buffer)[0], TestWav.Pcm16Sample(file, buffer * BufferSize, 0, channels: 2), precision: 4);
            Assert.Equal(Numbered(buffer)[0], TestWav.Pcm16Sample(file, buffer * BufferSize, 1, channels: 2), precision: 4);
        }
    }

    [Fact]
    public void BuffersDroppedWhileTheDiskStalls_BecomeSilenceInTheirPlace()
    {
        KeptStream stream = new();
        SessionRecorder recorder = new(stream, SampleRate, BufferSize);
        int ringBuffers = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)(SampleRate * SessionRecorder.RingSeconds)) / BufferSize;

        // Faster than the writer's first pass: both rings fill and the last 50 pairs are dropped.
        Feed(recorder.OnBuffer, ringBuffers + 50, Numbered);
        double ringSeconds = ringBuffers * BufferSize / (double)SampleRate;
        Assert.True(SpinWait.SpinUntil(() => recorder.Seconds >= ringSeconds - 1e-9, TimeSpan.FromSeconds(5)));
        Feed(recorder.OnBuffer, 10, buffer => Numbered(ringBuffers + 50 + buffer));
        recorder.Stop();

        byte[] file = stream.Contents;
        Assert.Equal(50, recorder.Overruns);
        Assert.Equal((ringBuffers + 60) * BufferSize * 4, (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(40)));
        for (int channel = 0; channel < 2; channel++)
        {
            Assert.Equal(Numbered(ringBuffers - 1)[0], TestWav.Pcm16Sample(file, (ringBuffers * BufferSize) - 1, channel, channels: 2), precision: 4);
            Assert.Equal(0, TestWav.Pcm16Sample(file, ringBuffers * BufferSize, channel, channels: 2), precision: 4);
            Assert.Equal(0, TestWav.Pcm16Sample(file, ((ringBuffers + 50) * BufferSize) - 1, channel, channels: 2), precision: 4);
            Assert.Equal(Numbered(ringBuffers + 50)[0], TestWav.Pcm16Sample(file, (ringBuffers + 50) * BufferSize, channel, channels: 2), precision: 4);
        }
    }

    [Fact]
    public void ASideOnAFasterClock_IsKeptWithinAFewBuffersOfTheOther()
    {
        KeptStream stream = new();
        SessionRecorder recorder = new(stream, SampleRate, BufferSize);

        // The input delivers 1 % more than the output, in real-time pieces the writer keeps up with.
        int input = 0;
        int output = 0;
        for (int round = 0; round < 40; round++)
        {
            int inputFrom = input;
            int outputFrom = output;
            Feed(recorder.OnInput, 101, buffer => Numbered(inputFrom + buffer));
            Feed(recorder.OnOutput, 100, buffer => Numbered(outputFrom + buffer));
            input += 101;
            output += 100;
            Thread.Sleep(60);
        }

        recorder.Stop();

        // The last output buffer is paired with one of the last input buffers (a window or two of lead may remain), not
        // with the input buffer of its own number, 40 buffers back.
        byte[] file = stream.Contents;
        int lastFrame = ((int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(40)) / 4) - 1;
        double paired = TestWav.Pcm16Sample(file, lastFrame, 0, channels: 2);
        Assert.Equal(Numbered(output - 1)[0], TestWav.Pcm16Sample(file, lastFrame, 1, channels: 2), precision: 4);
        Assert.Contains(Enumerable.Range(input - 12, 12), buffer => Math.Abs(Numbered(buffer)[0] - paired) < 1e-3);
    }

    [Fact]
    public void Drain_IsOnlyForARecorderWithoutABackgroundThreadThatHasNotStopped()
    {
        SessionRecorder background = new(new KeptStream(), SampleRate, BufferSize);
        SessionRecorder manual = new(new KeptStream(), SampleRate, BufferSize, background: false);
        manual.Drain();
        manual.Stop();

        Assert.Throws<InvalidOperationException>(background.Drain);
        Assert.Throws<InvalidOperationException>(manual.Drain);
        background.Stop();
    }

    [Fact]
    public void AudioSide_AllocatesNothing()
    {
        SessionRecorder recorder = new(new KeptStream(), SampleRate, BufferSize);
        float[] buffer = Ramp(0.001f);
        for (int i = 0; i < 100; i++)
        {
            recorder.OnBuffer(buffer, buffer);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            recorder.OnBuffer(buffer, buffer);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        recorder.Stop();

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void StuckWriter_NeverHoldsUpTheAudioAndCountsTheDroppedBuffers()
    {
        BlockingStream stream = new();
        SessionRecorder recorder = new(stream, SampleRate, BufferSize);
        float[] buffer = Ramp(0.001f);
        int buffersInTwoSeconds = 2 * SampleRate / BufferSize;

        // The writer is stuck on its first write; the audio side goes on and fills the ring.
        for (int i = 0; i < buffersInTwoSeconds; i++)
        {
            recorder.OnBuffer(buffer, buffer);
        }

        Assert.Equal(0, recorder.Overruns);
        for (int i = 0; i < buffersInTwoSeconds; i++)
        {
            recorder.OnBuffer(buffer, buffer);
        }

        Assert.True(recorder.Overruns > 0);
        stream.Release();
        recorder.Stop();
    }

    [Fact]
    public void FailingWriter_IsReportedWhenTheRecordingStops()
    {
        SessionRecorder recorder = new(new FailingStream(), SampleRate, BufferSize);
        recorder.OnBuffer(Ramp(0.001f), Ramp(0.001f));

        RecordingFailedException error = Assert.Throws<RecordingFailedException>(recorder.Stop);

        Assert.Equal("Inspelningen kunde inte skrivas.", error.Message);
    }

    [Fact]
    public void AnyDestinationFailure_IsAFailedRecordingNotACrash()
    {
        SessionRecorder recorder = new(new FailingStream(new ObjectDisposedException("fil")), SampleRate, BufferSize);
        recorder.OnBuffer(Ramp(0.001f), Ramp(0.001f));

        Assert.Throws<RecordingFailedException>(recorder.Stop);
    }

    [Fact]
    public void Dispose_NeverThrows()
    {
        SessionRecorder recorder = new(new FailingStream(), SampleRate, BufferSize);
        recorder.OnBuffer(Ramp(0.001f), Ramp(0.001f));

        recorder.Dispose();
    }

    [Fact]
    public void Stop_Twice_DoesNothingTheSecondTime()
    {
        SessionRecorder recorder = new(new KeptStream(), SampleRate, BufferSize);

        recorder.Stop();
        recorder.Stop();
    }

    private static void Feed(SpanAction write, int buffers, Func<int, float[]> buffer, Barrier? pace = null)
    {
        for (int i = 0; i < buffers; i++)
        {
            pace?.SignalAndWait();
            write(buffer(i));
        }
    }

    private static void Feed(PairAction write, int buffers, Func<int, float[]> buffer)
    {
        for (int i = 0; i < buffers; i++)
        {
            float[] samples = buffer(i);
            write(samples, samples);
        }
    }

    // A buffer whose every sample tells which buffer it is: steps of 1/400, well above 16-bit rounding, never 0.
    private static float[] Numbered(int buffer)
    {
        return [.. Enumerable.Repeat(((buffer % 200) + 1) / 400f, BufferSize)];
    }

    private static float[] Ramp(float step)
    {
        return [.. Enumerable.Range(0, BufferSize).Select(i => i * step)];
    }

    private delegate void SpanAction(ReadOnlySpan<float> samples);

    private delegate void PairAction(ReadOnlySpan<float> input, ReadOnlySpan<float> output);

    // Takes the header, then blocks every write until released, as a disk that has stopped.
    private sealed class BlockingStream : MemoryStream
    {
        private readonly ManualResetEventSlim _released = new();
        private int _writes;

        public void Release()
        {
            _released.Set();
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (++_writes > 1)
            {
                _released.Wait();
            }

            base.Write(buffer);
        }
    }

    // Takes the header, then fails every write.
    private sealed class FailingStream(Exception? failure = null) : MemoryStream
    {
        private int _writes;

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (++_writes > 1)
            {
                throw failure ?? new IOException("Disken är full.");
            }

            base.Write(buffer);
        }
    }
}
