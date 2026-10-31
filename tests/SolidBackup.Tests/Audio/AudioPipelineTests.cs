using SolidBackup.Audio;

namespace SolidBackup.Tests.Audio;

public sealed class AudioPipelineTests : IDisposable
{
    private const int SampleRate = 48_000;
    private const int BufferSize = 64;

    private readonly List<string> _files = [];

    public void Dispose()
    {
        _files.ForEach(File.Delete);
    }

    [Fact]
    public void Pcm16WavFile_PassesThroughUnchanged_PaddedToWholeBuffers()
    {
        float[] tone = Sine(frames: (BufferSize * 10) + 17);
        string path = Temp(TestWav.Pcm16(SampleRate, 1, tone));
        using WavFileInput input = new(path, SampleRate, BufferSize);
        using MemoryAudioOutput output = new(SampleRate, BufferSize, capacityFrames: BufferSize * 11);

        AudioPipeline pipeline = new(input, output, new PassThroughProcessor());
        pipeline.Start();
        pipeline.Stop();

        Assert.Equal(BufferSize * 11, output.Samples.Length);
        for (int i = 0; i < tone.Length; i++)
        {
            Assert.Equal(tone[i], output.Samples[i], 1f / 32768);
        }

        Assert.All(output.Samples[tone.Length..].ToArray(), sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void Float32WavFile_PassesThroughExactly()
    {
        float[] tone = Sine(frames: BufferSize * 4);
        string path = Temp(TestWav.Float32(SampleRate, 1, tone));
        using WavFileInput input = new(path, SampleRate, BufferSize);
        using MemoryAudioOutput output = new(SampleRate, BufferSize, capacityFrames: tone.Length);

        new AudioPipeline(input, output, new PassThroughProcessor()).Start();

        Assert.Equal(tone, output.Samples.ToArray());
    }

    [Fact]
    public void PerBufferPath_AllocatesNothing()
    {
        string path = Temp(TestWav.Float32(SampleRate, 1, Sine(BufferSize)));
        using WavFileInput input = new(path, SampleRate, BufferSize);
        using MemoryAudioOutput output = new(SampleRate, BufferSize, capacityFrames: BufferSize * 1100);
        AudioPipeline pipeline = new(input, output, new PassThroughProcessor());
        float[] buffer = Sine(BufferSize);
        output.Start();
        for (int i = 0; i < 100; i++)
        {
            pipeline.OnBuffer(buffer);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            pipeline.OnBuffer(buffer);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void StopFromHandler_StopsTheWavInput()
    {
        string path = Temp(TestWav.Float32(SampleRate, 1, Sine(BufferSize * 5)));
        using WavFileInput input = new(path, SampleRate, BufferSize);
        StoppingHandler handler = new(input, stopAfter: 2);

        input.Start(handler);
        input.Stop();

        Assert.Equal(2, handler.Buffers);
    }

    [Fact]
    public void StopTwiceThenDispose_IsHarmless()
    {
        string path = Temp(TestWav.Float32(SampleRate, 1, Sine(BufferSize)));
        WavFileInput input = new(path, SampleRate, BufferSize);
        MemoryAudioOutput output = new(SampleRate, BufferSize, capacityFrames: BufferSize);
        AudioPipeline pipeline = new(input, output, new PassThroughProcessor());
        pipeline.Start();

        pipeline.Stop();
        pipeline.Stop();
        input.Dispose();
        output.Dispose();

        Assert.Equal(BufferSize, output.Samples.Length);
        Assert.Throws<InvalidOperationException>(() => output.Write(new float[BufferSize]));
    }

    [Fact]
    public void WavFileWithOtherSampleRate_ThrowsWithoutResampling()
    {
        string path = Temp(TestWav.Float32(44_100, 1, 0f));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => new WavFileInput(path, SampleRate, BufferSize));

        Assert.Contains("44100 Hz men 48000 Hz krävs", exception.Message);
    }

    [Fact]
    public void MismatchedInputAndOutput_Throws()
    {
        string path = Temp(TestWav.Float32(SampleRate, 1, 0f));
        using WavFileInput input = new(path, SampleRate, BufferSize);
        using MemoryAudioOutput output = new(SampleRate, BufferSize * 2, capacityFrames: BufferSize);

        Assert.Throws<ArgumentException>(() => new AudioPipeline(input, output, new PassThroughProcessor()));
    }

    [Fact]
    public void InputFailsToStart_StopsTheOutputAgain()
    {
        RecordingEndpoint endpoint = new(failInputStart: true);
        AudioPipeline pipeline = new(endpoint, endpoint, new PassThroughProcessor());

        Assert.Throws<IOException>(pipeline.Start);

        Assert.Equal(["output start", "output stop"], endpoint.Calls);
    }

    [Fact]
    public void Stop_StopsTheOutputBeforeTheInput()
    {
        RecordingEndpoint endpoint = new(failInputStart: false);
        AudioPipeline pipeline = new(endpoint, endpoint, new PassThroughProcessor());

        pipeline.Start();
        pipeline.Stop();

        Assert.Equal(["output start", "input start", "output stop", "input stop"], endpoint.Calls);
    }

    private static float[] Sine(int frames)
    {
        return Enumerable.Range(0, frames).Select(i => 0.5f * MathF.Sin(2 * MathF.PI * 440 * i / SampleRate)).ToArray();
    }

    private string Temp(byte[] wav)
    {
        string path = TestWav.WriteTemp(wav);
        _files.Add(path);
        return path;
    }

    private sealed class RecordingEndpoint(bool failInputStart) : IAudioInput, IAudioOutput
    {
        public List<string> Calls { get; } = [];

        public int SampleRate => AudioPipelineTests.SampleRate;

        public int BufferSize => AudioPipelineTests.BufferSize;

        public void Start(IAudioInputHandler handler)
        {
            if (failInputStart)
            {
                throw new IOException("no input");
            }

            Calls.Add("input start");
        }

        void IAudioInput.Stop()
        {
            Calls.Add("input stop");
        }

        public void Start()
        {
            Calls.Add("output start");
        }

        void IAudioOutput.Stop()
        {
            Calls.Add("output stop");
        }

        public void Write(ReadOnlySpan<float> samples)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class StoppingHandler(IAudioInput input, int stopAfter) : IAudioInputHandler
    {
        public int Buffers { get; private set; }

        public void OnBuffer(ReadOnlySpan<float> samples)
        {
            if (++Buffers == stopAfter)
            {
                input.Stop();
            }
        }
    }
}
