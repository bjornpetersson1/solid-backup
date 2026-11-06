using SolidBackup.Audio;
using SolidBackup.Import;
using SolidBackup.Tests.Audio;

namespace SolidBackup.Tests.Import;

public sealed class ClipImporterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"solid-backup-import-{Guid.NewGuid():N}");

    public ClipImporterTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    public static TheoryData<string, int, int> WavFormats => new()
    {
        { "pcm16", 48_000, 1 },
        { "pcm16", 44_100, 2 },
        { "pcm24", 44_100, 1 },
        { "pcm24", 48_000, 2 },
        { "pcm32", 48_000, 2 },
        { "float32", 44_100, 2 },
    };

    [Theory]
    [MemberData(nameof(WavFormats))]
    public void AWav_IsReadAtItsOwnRate_WithStereoAveraged(string encoding, int sampleRate, int channels)
    {
        // Left 0.5, right 0 for stereo: averaged to 0.25.
        float[] frames = channels == 1 ? [0.25f, 0.25f, 0.25f] : [0.5f, 0f, 0.5f, 0f, 0.5f, 0f];
        string path = Write("klipp.wav", Wav(encoding, sampleRate, channels, frames));

        ImportedClip clip = ClipImporter.Load(path, 48_000, new FakeConverter());

        Assert.Equal("klipp", clip.Name);
        Assert.Equal(sampleRate, clip.Audio.SampleRate);
        Assert.All(clip.Audio.Samples, sample => Assert.Equal(0.25f, sample, 1e-4f));
        Assert.Equal(3, clip.Audio.Samples.Length);
    }

    [Theory]
    [InlineData(8_000)]
    [InlineData(22_050)]
    [InlineData(96_000)]
    public void AWavAtARateTheAppDoesNotRunAt_IsConvertedToTheAppsRate(int sampleRate)
    {
        string path = Write("annan.wav", TestWav.Pcm16(sampleRate, 2, 0.5f, 0f, 0.5f, 0f));
        FakeConverter converter = new();

        ImportedClip clip = ClipImporter.Load(path, 48_000, converter);

        Assert.Equal((path, 48_000), (converter.Source, converter.SampleRate));
        Assert.Equal(48_000, clip.Audio.SampleRate);
    }

    [Theory]
    [InlineData(".m4a")]
    [InlineData(".MP3")]
    [InlineData(".aiff")]
    [InlineData(".caf")]
    public void AnotherFormat_IsConvertedAtTheAppsRate_AndItsTemporaryFileRemoved(string extension)
    {
        string path = Write("röstmemo" + extension, [1, 2, 3]);
        FakeConverter converter = new();

        ImportedClip clip = ClipImporter.Load(path, 44_100, converter);

        Assert.Equal("röstmemo", clip.Name);
        Assert.Equal(44_100, clip.Audio.SampleRate);
        Assert.Equal((path, 44_100), (converter.Source, converter.SampleRate));
        Assert.False(File.Exists(converter.Destination));
    }

    [Fact]
    public void AMissingFile_IsASwedishError()
    {
        AssertFails(Path.Combine(_folder, "saknas.wav"), "finns inte");
    }

    [Fact]
    public void AnUnknownExtension_IsASwedishError()
    {
        AssertFails(Write("anteckning.txt", [1]), "stöds inte");
        AssertFails(Write("utan", [1]), "utan ändelse");
    }

    [Fact]
    public void AWavWithThreeChannelsOrAnUnsupportedEncoding_IsASwedishError()
    {
        AssertFails(Write("tre.wav", TestWav.Pcm16(48_000, 3, 0f, 0f, 0f)), "kanaler");
        AssertFails(Write("åtta.wav", TestWav.Build(TestWav.Pcm, 48_000, 1, 8, [128])), "stöds inte");
    }

    [Fact]
    public void AFailedConversion_IsItsSwedishError_AndLeavesNoTemporaryFile()
    {
        FakeConverter converter = new() { Fails = true };

        ImportFailedException exception = Assert.Throws<ImportFailedException>(() => ClipImporter.Load(Write("trasig.m4a", [1]), 48_000, converter));

        Assert.Equal(FakeConverter.FailureMessage, exception.Message);
        Assert.False(File.Exists(converter.Destination));
    }

    [Fact]
    public void AMissingAfconvert_IsASwedishError()
    {
        AfconvertConverter missing = new(Path.Combine(_folder, "afconvert"));

        ImportFailedException exception = Assert.Throws<ImportFailedException>(() => ClipImporter.Load(Write("memo.m4a", [1]), 48_000, missing));

        Assert.Contains("saknas", exception.Message);
    }

    [Fact]
    public void AnEmptyClip_IsASwedishError()
    {
        AssertFails(Write("tom.wav", TestWav.Pcm16(48_000, 1)), "inget ljud");
    }

    [Fact]
    public void AWavOverTwentyMinutes_IsRefusedFromItsHeader_BeforeItIsDecoded()
    {
        // The header and then a hole: the file is as long as its header says, without writing its data.
        int rate = 48_000;
        long dataBytes = ((ClipImporter.MaxMinutes * 60) + 1L) * rate * 2;
        byte[] header = TestWav.Build(TestWav.Pcm, rate, 1, 16, []);
        BitConverter.GetBytes((uint)dataBytes).CopyTo(header, header.Length - 4);
        string path = Write("lång.wav", header);
        using (FileStream stream = new(path, FileMode.Open))
        {
            stream.SetLength(header.Length + dataBytes);
        }

        AssertFails(path, $"längre än {ClipImporter.MaxMinutes} minuter");
    }

    [Fact]
    public void AConvertedClipOverTwentyMinutes_IsASwedishError()
    {
        FakeConverter converter = new() { Seconds = (ClipImporter.MaxMinutes * 60) + 1 };

        ImportFailedException exception = Assert.Throws<ImportFailedException>(() => ClipImporter.Load(Write("lång.m4a", [1]), 8_000, converter));

        Assert.Contains($"längre än {ClipImporter.MaxMinutes} minuter", exception.Message);
    }

    [Fact]
    public void ACancelledConversion_IsACancellation_AndLeavesNoTemporaryFile()
    {
        FakeConverter converter = new();
        using CancellationTokenSource cancel = new();
        cancel.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => ClipImporter.Load(Write("memo.m4a", [1]), 48_000, converter, cancel.Token));
        Assert.False(File.Exists(converter.Destination));
    }

    [AfconvertFact]
    public void Afconvert_ConvertsAnAiffClipToMonoAtTheAppsRate()
    {
        // afconvert itself makes the AIFF from a WAV the app writes (with complete sizes, which afconvert needs), then
        // the importer converts it back.
        string wav = Path.Combine(_folder, "källa.wav");
        WavWriter.WritePcm16(wav, 44_100, [.. Enumerable.Repeat(0.5f, 4410)]);
        string aiff = Path.Combine(_folder, "klipp.aiff");
        using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(AfconvertConverter.Program, ["-f", "AIFF", "-d", "BEI16", wav, aiff]))
        {
            process.WaitForExit();
        }

        ImportedClip clip = ClipImporter.Load(aiff, 48_000, new AfconvertConverter());

        Assert.Equal(48_000, clip.Audio.SampleRate);
        Assert.InRange(clip.Audio.Samples.Length, 4700, 4900);
        Assert.InRange(clip.Audio.Samples[2000], 0.45f, 0.55f);
    }

    private static byte[] Wav(string encoding, int sampleRate, int channels, float[] interleaved)
    {
        return encoding switch
        {
            "pcm16" => TestWav.Pcm16(sampleRate, channels, interleaved),
            "float32" => TestWav.Float32(sampleRate, channels, interleaved),
            "pcm24" => TestWav.Pcm24(sampleRate, channels, interleaved),
            _ => TestWav.Pcm32(sampleRate, channels, interleaved),
        };
    }

    private void AssertFails(string path, string says)
    {
        ImportFailedException exception = Assert.Throws<ImportFailedException>(() => ClipImporter.Load(path, 48_000, new FakeConverter()));
        Assert.Contains(says, exception.Message);
    }

    private string Write(string name, byte[] contents)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, contents);
        return path;
    }
}

/// <summary>Runs only where <c>/usr/bin/afconvert</c> is, that is on macOS.</summary>
public sealed class AfconvertFactAttribute : FactAttribute
{
    public AfconvertFactAttribute()
    {
        if (!File.Exists(AfconvertConverter.Program))
        {
            Skip = $"{AfconvertConverter.Program} finns bara på macOS.";
        }
    }
}

/// <summary>A converter that writes a short mono WAV at the asked rate, or fails, without starting a process.</summary>
public sealed class FakeConverter : IAudioConverter
{
    public const string FailureMessage = "Filen kunde inte konverteras (testfel).";

    public bool Fails { get; init; }

    public string? Source { get; private set; }

    public string? Destination { get; private set; }

    public int SampleRate { get; private set; }

    /// <summary>How long the converted clip is, in seconds; three samples when not given.</summary>
    public int? Seconds { get; init; }

    public void ConvertToWav(string source, string destination, int sampleRate, CancellationToken cancellation)
    {
        (Source, Destination, SampleRate) = (source, destination, sampleRate);
        File.WriteAllBytes(destination, [0]);
        cancellation.ThrowIfCancellationRequested();
        if (Fails)
        {
            throw new ImportFailedException(FailureMessage);
        }

        float[] samples = Seconds is { } seconds ? new float[seconds * sampleRate] : [0.1f, 0.2f, 0.3f];
        File.WriteAllBytes(destination, TestWav.Pcm16(sampleRate, 1, samples));
    }
}
