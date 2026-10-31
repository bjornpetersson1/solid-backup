using System.Buffers.Binary;
using System.Text;

using SolidBackup.Measurement;

namespace SolidBackup.Tests.Measurement;

public sealed class RecordedCorpusTests
{
    private const long MaxCorpusBytes = 10L * 1024 * 1024;

    private static readonly CorpusDirectory Corpus = CorpusDirectory.Locate();

    public static TheoryData<string> Clips()
    {
        TheoryData<string> clips = [];
        foreach (ScriptedClip clip in RecordingScript.Read(Corpus.ScriptPath))
        {
            clips.Add(clip.Name);
        }

        return clips;
    }

    [Theory]
    [MemberData(nameof(Clips))]
    public void Recording_Is48kHzMono16BitPcm(string clip)
    {
        string path = Corpus.WavPath(clip);
        Assert.True(File.Exists(path), $"{path} is missing");

        (ushort formatTag, ushort channels, uint sampleRate, ushort bits) = ReadFormat(path);

        Assert.Equal((1, 1, 48_000u, 16), (formatTag, channels, sampleRate, bits));
    }

    [Theory]
    [MemberData(nameof(Clips))]
    public void Reference_CoversTheRecordingAndMatchesTheScript(string name)
    {
        Assert.True(File.Exists(Corpus.ReferencePath(name)), $"{Corpus.ReferencePath(name)} is missing");
        ScriptedClip scripted = RecordingScript.Read(Corpus.ScriptPath).Single(clip => clip.Name == name);
        CorpusClip clip = RecordedCorpus.Load(Corpus, name);

        ReferenceChecks.AssertCovers(clip.Reference, clip.Samples.Length * 1000.0 / clip.SampleRate, name);
        ReferenceSegment[] sung = clip.Reference.SungSegments().ToArray();
        Assert.Equal(scripted.Notes.Count, sung.Length);
        for (int i = 0; i < sung.Length; i++)
        {
            int midi = scripted.Notes[i];
            Assert.Equal(CorpusRange.Contains(midi) ? SegmentKind.Note : SegmentKind.OutOfRange, sung[i].Kind);
            Assert.InRange(sung[i].MedianPitch, (midi * 100) - MeasurementTargets.PitchToleranceCents, (midi * 100) + MeasurementTargets.PitchToleranceCents);
        }

        Assert.Equal(scripted.Notes.Count(CorpusRange.Contains), clip.Reference.OnsetsMs.Count);
        if (!scripted.IsPitched)
        {
            Assert.All(clip.Reference.Segments, segment => Assert.True(segment.IsUnvoiced));
        }
    }

    [Fact]
    public void OracleDetector_ScoresPerfectlyOnTheRecordings()
    {
        CorpusScore score = CorpusScorer.Score(RecordedCorpus.Load(Corpus), clip => new OracleDetector(clip.Reference, clip.SampleRate), bufferSize: 64);

        Assert.Equal(1, score.Total.PitchAccuracy);
        Assert.Equal(0, score.Total.FalseNotes);
        Assert.Equal(1, score.Total.OnsetAccuracy);
        Assert.True(score.Total.UnvoicedWindows > 0);
    }

    [Fact]
    public void Corpus_IsAtMost10MB()
    {
        long bytes = new DirectoryInfo(Corpus.Root).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);

        Assert.True(bytes <= MaxCorpusBytes, $"The corpus holds {bytes} bytes; the limit is {MaxCorpusBytes}.");
    }

    [Fact]
    public void Script_HasVoiceWhistleSilenceAndBreath()
    {
        IReadOnlyList<ScriptedClip> script = RecordingScript.Read(Corpus.ScriptPath);

        Assert.All(Enum.GetValues<RecordingKind>(), kind => Assert.Contains(script, clip => clip.Kind == kind));
        Assert.All(
            script.Where(clip => clip.Kind == RecordingKind.Whistle).SelectMany(clip => clip.Notes),
            midi => Assert.InRange(midi, 67, CorpusRange.HighestMidi));
    }

    private static (ushort FormatTag, ushort Channels, uint SampleRate, ushort Bits) ReadFormat(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        int position = 12;
        while (position + 8 <= file.Length)
        {
            string id = Encoding.ASCII.GetString(file, position, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(position + 4));
            if (id == "fmt ")
            {
                ReadOnlySpan<byte> format = file.AsSpan(position + 8);
                return (
                    BinaryPrimitives.ReadUInt16LittleEndian(format),
                    BinaryPrimitives.ReadUInt16LittleEndian(format[2..]),
                    BinaryPrimitives.ReadUInt32LittleEndian(format[4..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(format[14..]));
            }

            position += 8 + size + (size & 1);
        }

        throw new InvalidDataException($"{path} has no format chunk.");
    }
}
