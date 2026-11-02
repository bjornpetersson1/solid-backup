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

    private static float[] Ramp(float step)
    {
        return [.. Enumerable.Range(0, BufferSize).Select(i => i * step)];
    }

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
