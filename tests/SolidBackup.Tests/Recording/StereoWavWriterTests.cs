using System.Buffers.Binary;
using System.Text;

using SolidBackup.Audio;
using SolidBackup.Recording;
using SolidBackup.Tests.Audio;

namespace SolidBackup.Tests.Recording;

public sealed class StereoWavWriterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"solid-backup-{Guid.NewGuid():N}.wav");

    public void Dispose()
    {
        File.Delete(_path);
    }

    [Fact]
    public void ClosedFile_HasAStandardHeaderAndTheSamplesWritten()
    {
        float[] interleaved = [0.5f, -0.25f, 0.1f, 0.9f, -1f, 0f];
        using (StereoWavWriter writer = new(File.Create(_path), 48_000, maxSamples: 6))
        {
            writer.Write(interleaved);
        }

        byte[] file = File.ReadAllBytes(_path);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(file, 0, 4));
        Assert.Equal((uint)(file.Length - 8), BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(4)));
        Assert.Equal("WAVEfmt ", Encoding.ASCII.GetString(file, 8, 8));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(20)));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(22)));
        Assert.Equal(48_000u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(24)));
        Assert.Equal(16, BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(34)));
        Assert.Equal("data", Encoding.ASCII.GetString(file, 36, 4));
        Assert.Equal(12u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(40)));
        for (int i = 0; i < interleaved.Length; i++)
        {
            Assert.Equal(interleaved[i], TestWav.Pcm16Sample(file, i / 2, i % 2, channels: 2), precision: 4);
        }

        // The app's own reader mixes stereo down to mono.
        WavAudio audio = WavReader.Read(_path);
        Assert.Equal([(0.5f - 0.25f) / 2, (0.1f + 0.9f) / 2, -0.5f], audio.Samples, new FloatComparer(1e-4f));
    }

    [Fact]
    public void FileWhileRecording_CanBeRead()
    {
        using FileStream stream = new(_path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        StereoWavWriter writer = new(stream, 48_000, maxSamples: 4);
        writer.Write([0.5f, 0.5f, 0.25f, 0.25f]);
        stream.Flush();

        WavAudio audio = WavReader.Read(_path);

        Assert.Equal([0.5f, 0.25f], audio.Samples, new FloatComparer(1e-4f));
        writer.Dispose();
    }

    [Fact]
    public void AtTheSizeLimit_StopsWritingAndSaysSo()
    {
        KeptStream stream = new();
        StereoWavWriter writer = new(stream, 48_000, maxSamples: 8, maxDataBytes: 12);

        writer.Write([0.1f, 0.1f, 0.2f, 0.2f]);
        Assert.False(writer.IsFull);
        writer.Write([0.3f, 0.3f, 0.4f, 0.4f]);
        writer.Dispose();

        Assert.True(writer.IsFull);
        Assert.Equal(3, writer.Frames);
        Assert.Equal(12u, BinaryPrimitives.ReadUInt32LittleEndian(stream.Contents.AsSpan(40)));
    }

    private sealed class FloatComparer(float tolerance) : IEqualityComparer<float>
    {
        public bool Equals(float x, float y)
        {
            return Math.Abs(x - y) <= tolerance;
        }

        public int GetHashCode(float value)
        {
            return 0;
        }
    }
}
