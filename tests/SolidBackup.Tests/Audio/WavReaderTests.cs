using SolidBackup.Audio;

namespace SolidBackup.Tests.Audio;

public class WavReaderTests
{
    [Fact]
    public void Read_Pcm16Mono_ReturnsSamplesWithinQuantisation()
    {
        float[] expected = [0f, 0.5f, -0.5f, 0.25f];

        WavAudio audio = Read(TestWav.Pcm16(48_000, 1, expected));

        Assert.Equal(48_000, audio.SampleRate);
        Assert.Equal(expected.Length, audio.Samples.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], audio.Samples[i], 1f / 32768);
        }
    }

    [Fact]
    public void Read_Float32Stereo_MixesDownToMono()
    {
        WavAudio audio = Read(TestWav.Float32(44_100, 2, 0.2f, 0.4f, -1f, 1f));

        Assert.Equal(44_100, audio.SampleRate);
        Assert.Equal([0.3f, 0f], audio.Samples, (a, b) => Math.Abs(a - b) < 1e-6);
    }

    [Fact]
    public void Read_SkipsUnknownChunksIncludingPadByte()
    {
        byte[] wav = TestWav.Build(TestWav.Float, 48_000, 1, 32, BitConverter.GetBytes(0.75f), withListChunk: true);

        Assert.Equal([0.75f], Read(wav).Samples);
    }

    [Theory]
    [InlineData(TestWav.Pcm, 8)]
    [InlineData(TestWav.Float, 64)]
    [InlineData((ushort)2, 16)]
    public void Read_UnsupportedFormat_ThrowsSwedishMessage(ushort formatTag, int bitsPerSample)
    {
        byte[] wav = TestWav.Build(formatTag, 48_000, 1, bitsPerSample, new byte[bitsPerSample / 8]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Read(wav));

        Assert.StartsWith("WAV-formatet stöds inte", exception.Message);
    }

    [Fact]
    public void Read_24And32BitPcm_GivesTheSamples()
    {
        // Half scale up and down in 24 bits (0x400000) and in 32 bits (0x40000000), little-endian.
        byte[] pcm24 = TestWav.Build(TestWav.Pcm, 96_000, 1, 24, [0x00, 0x00, 0x40, 0x00, 0x00, 0xC0]);
        byte[] pcm32 = TestWav.Build(TestWav.Pcm, 8_000, 1, 32, [0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0xC0]);

        Assert.Equal([0.5f, -0.5f], Read(pcm24).Samples);
        Assert.Equal([0.5f, -0.5f], Read(pcm32).Samples);
        Assert.Equal(96_000, Read(pcm24).SampleRate);
    }

    [Fact]
    public void Read_UnknownDataLength_ReadsToTheEndOfTheFile()
    {
        byte[] wav = TestWav.Float32(48_000, 1, 0.25f, -0.25f);
        BitConverter.GetBytes(uint.MaxValue).CopyTo(wav, wav.Length - 12);

        Assert.Equal([0.25f, -0.25f], Read(wav).Samples);
    }

    [Fact]
    public void Read_ThreeChannels_Throws()
    {
        byte[] wav = TestWav.Build(TestWav.Pcm, 48_000, 3, 16, new byte[6]);

        Assert.Contains("3 kanaler", Assert.Throws<InvalidDataException>(() => Read(wav)).Message);
    }

    [Fact]
    public void Read_NotAWavFile_Throws()
    {
        Assert.Contains("RIFF", Assert.Throws<InvalidDataException>(() => Read("hello, world"u8.ToArray())).Message);
    }

    [Fact]
    public void Read_TruncatedHeader_Throws()
    {
        byte[] wav = TestWav.Pcm16(48_000, 1, 0.5f)[..30];

        Assert.Throws<InvalidDataException>(() => Read(wav));
    }

    [Theory]
    [InlineData(WavChannel.Mixed, 0.125f)]
    [InlineData(WavChannel.Left, 0.5f)]
    [InlineData(WavChannel.Right, -0.25f)]
    public void Read_AStereoFile_GivesTheChosenChannel(WavChannel channel, float expected)
    {
        byte[] wav = TestWav.Float32(48_000, 2, 0.5f, -0.25f, 0.5f, -0.25f);

        using MemoryStream stream = new(wav);
        WavAudio audio = WavReader.Read(stream, channel);

        Assert.Equal([expected, expected], audio.Samples);
    }

    [Fact]
    public void Read_AMonoFile_GivesItsOneChannelWhicheverIsChosen()
    {
        using MemoryStream stream = new(TestWav.Float32(48_000, 1, 0.5f, 0.25f));

        Assert.Equal([0.5f, 0.25f], WavReader.Read(stream, WavChannel.Right).Samples);
    }

    [Fact]
    public void ReadInfo_GivesTheFormatAndLength()
    {
        string path = TestWav.WriteTemp(TestWav.Pcm16(22_050, 2, new float[2 * 44_100]));
        try
        {
            WavInfo info = WavReader.ReadInfo(path);

            Assert.Equal(new WavInfo(22_050, 2, 44_100), info);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static WavAudio Read(byte[] wav)
    {
        using MemoryStream stream = new(wav);
        return WavReader.Read(stream);
    }
}
