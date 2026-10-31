using System.Globalization;

using SolidBackup.Audio;
using SolidBackup.Configuration;
using SolidBackup.Measurement;

namespace SolidBackup.CorpusTool;

/// <summary>Developer commands for the test corpus; see <c>corpus/README.md</c>.</summary>
public static class Program
{
    private const string Usage = """
        Användning:
          tones <mapp> [<samplingsfrekvens>]   skriv de genererade tonerna som WAV med referensfiler
          suggest <klipp> ...                  föreslå en tonlista för inspelningar som inte står i listan än
          label [<klipp> ...]                  märk inspelningarna (alla, eller de angivna) och skriv .ref.csv
          report                               mät tonhöjd, grind och ansatser på hela korpusen och skriv en rapport i markdown
        """;

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FormatException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Fel: {exception.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        string command = args.Length > 0 ? args[0] : "";
        string[] rest = args.Length > 0 ? args[1..] : [];
        return command switch
        {
            "tones" when rest.Length is 1 or 2 => WriteTones(rest[0], rest.Length == 2 ? ParseSampleRate(rest[1]) : 48_000),
            "suggest" when rest.Length > 0 => Suggest(rest),
            "label" => Label(rest),
            "report" when rest.Length == 0 => Report(),
            _ => ShowUsage(),
        };
    }

    private static int WriteTones(string folder, int sampleRate)
    {
        Directory.CreateDirectory(folder);
        foreach (CorpusClip clip in GeneratedCorpus.Create(sampleRate).Concat(GeneratedCorpus.Melodies(sampleRate)))
        {
            WavWriter.WritePcm16(Path.Combine(folder, clip.Name + ".wav"), sampleRate, clip.Samples);
            ReferenceFile.Write(Path.Combine(folder, clip.Name + ".ref.csv"), clip.Reference);
            Console.WriteLine($"{clip.Name}: {clip.Reference.DurationMs / 1000:0.0} s");
        }

        return 0;
    }

    private static int Suggest(string[] names)
    {
        CorpusDirectory corpus = CorpusDirectory.Locate(Directory.GetCurrentDirectory());
        foreach (string name in names)
        {
            WavAudio audio = WavReader.Read(corpus.WavPath(name));
            IEnumerable<string> notes = ReferenceLabeler.SuggestNotes(audio.Samples, audio.SampleRate).Select(NoteName.Format);
            Console.WriteLine($"{name}: {string.Join(" ", notes)}");
        }

        return 0;
    }

    private static int Report()
    {
        AppSettings settings = new SettingsStore(SettingsStore.BundledDefaultsPath, "").Load().Settings;
        CorpusDirectory corpus = CorpusDirectory.Locate(Directory.GetCurrentDirectory());
        IEnumerable<CorpusClip> clips = GeneratedCorpus.Create(settings.SampleRate).Concat(RecordedCorpus.Load(corpus));
        Console.WriteLine(CorpusMeasurement.Run(clips, settings).ToMarkdown());
        return 0;
    }

    private static int Label(string[] names)
    {
        CorpusDirectory corpus = CorpusDirectory.Locate(Directory.GetCurrentDirectory());
        IReadOnlyList<ScriptedClip> script = RecordingScript.Read(corpus.ScriptPath);
        IEnumerable<ScriptedClip> clips = names.Length == 0
            ? script
            : names.Select(name => script.FirstOrDefault(clip => clip.Name == name)
                ?? throw new InvalidDataException($"Klippet {name} finns inte i tonlistan."));
        foreach (ScriptedClip clip in clips)
        {
            WavAudio audio = WavReader.Read(corpus.WavPath(clip.Name));
            ClipReference reference = ReferenceLabeler.Label(audio.Samples, audio.SampleRate, clip);
            ReferenceFile.Write(corpus.ReferencePath(clip.Name), reference);
            Console.WriteLine($"{clip.Name}: {Describe(clip, reference)}");
        }

        return 0;
    }

    private static string Describe(ScriptedClip clip, ClipReference reference)
    {
        if (!clip.IsPitched)
        {
            string kind = clip.Kind == RecordingKind.Breath ? "andning" : "tystnad";
            return $"{kind}, {reference.DurationMs / 1000:0.0} s";
        }

        IEnumerable<string> notes = reference.SungSegments().Zip(clip.Notes, (segment, midi) =>
            $"{segment.MedianPitch - (midi * 100.0):+0;-0;0} c");
        return $"{clip.Notes.Count} toner, {reference.OnsetsMs.Count} ansatser, medianens avvikelse från listan: {string.Join(" ", notes)}";
    }

    private static int ParseSampleRate(string text)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int rate) && rate is >= 8_000 and <= 192_000
            ? rate
            : throw new FormatException($"Samplingsfrekvensen '{text}' är inte ett heltal mellan 8000 och 192000.");
    }

    private static int ShowUsage()
    {
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
