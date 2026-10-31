using SolidBackup.Audio;

namespace SolidBackup.Tests.Audio;

public sealed class WavWriterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");

    public void Dispose()
    {
        File.Delete(_path);
    }

    [Fact]
    public void WrittenFile_ReadsBackWithin16BitQuantisation_ClippedToOne()
    {
        float[] samples = [0f, 0.25f, -0.5f, 0.999f, 1.5f, -1.5f];

        WavWriter.WritePcm16(_path, 44_100, samples);
        WavAudio audio = WavReader.Read(_path);

        Assert.Equal(44_100, audio.SampleRate);
        float[] expected = [0f, 0.25f, -0.5f, 0.999f, 1f, -1f];
        Assert.Equal(expected.Length, audio.Samples.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], audio.Samples[i], 2f / 32768);
        }
    }
}
