using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

using SolidBackup.Configuration;

using static System.FormattableString;

namespace SolidBackup.Measurement;

/// <summary>The analysis components (pitch, level, gate and onsets) scored on corpus clips, with the analysis time they took.</summary>
public sealed record CorpusMeasurement(CorpusScore Score, long Buffers, TimeSpan AnalysisTime, double AudioSeconds)
{
    public double MicrosecondsPerBuffer => Buffers == 0 ? 0 : AnalysisTime.TotalMicroseconds / Buffers;

    /// <summary>
    /// Runs a fresh detector made from <paramref name="settings"/> over each clip, at the clip's sample rate.
    /// In parallel the clips finish sooner, but the time per buffer is inflated by the threads sharing the CPU.
    /// </summary>
    public static CorpusMeasurement Run(IEnumerable<CorpusClip> clips, AppSettings settings, bool parallel = false)
    {
        ConcurrentBag<AnalysisCorpusDetector> detectors = [];
        CorpusClip[] all = clips.ToArray();
        CorpusScore score = CorpusScorer.Score(
            all,
            clip =>
            {
                AnalysisCorpusDetector detector = new(settings with { SampleRate = clip.SampleRate });
                detectors.Add(detector);
                return detector;
            },
            settings.BufferSize,
            parallel);
        return new CorpusMeasurement(
            score,
            detectors.Sum(detector => detector.Buffers),
            TimeSpan.FromTicks(detectors.Sum(detector => detector.AnalysisTime.Ticks)),
            all.Sum(clip => clip.Samples.Length / (double)clip.SampleRate));
    }

    /// <summary>
    /// The report as markdown: a pitch table and a level table, per clip and in total, each figure with its
    /// counts, then the targets and the analysis time.
    /// </summary>
    public string ToMarkdown()
    {
        StringBuilder text = new();
        text.AppendLine("### Tonhöjd");
        text.AppendLine();
        text.AppendLine("| Klipp | Röstade fönster | Träffsäkerhet | Oktavfel | Tonlösa fönster | Falska toner |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        AppendRows(text, PitchRow);
        text.AppendLine();
        text.AppendLine("### Nivå, grind och ansatser");
        text.AppendLine();
        text.AppendLine(Invariant($"| Klipp | Fönster i tystnad/andning | Toner där (grind på) | Rätta toner som grinden stänger av | Ansatser | Funna inom {MeasurementTargets.OnsetToleranceMs} ms | Extra ansatser (i tystnad/andning) |"));
        text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        AppendRows(text, LevelRow);
        text.AppendLine();

        ScoreCounts total = Score.Total;
        text.AppendLine($"- Träffsäkerhet ≥ {Percent(MeasurementTargets.MinPitchAccuracy, "0")}: {Verdict(total.MeetsPitchAccuracy)}");
        text.AppendLine($"- Oktavfel ≤ {Percent(MeasurementTargets.MaxOctaveErrors, "0")}: {Verdict(total.MeetsOctaveErrors)}");
        text.AppendLine($"- Falska toner ≤ {Percent(MeasurementTargets.MaxFalseNotes, "0")}: {Verdict(total.MeetsFalseNotes)}");
        text.AppendLine($"- Inga toner i tystnad och andning med grinden på: {Verdict(total.MeetsNoNotesInSilenceOrBreath)}");
        text.AppendLine(Invariant($"- Ansatser inom {MeasurementTargets.OnsetToleranceMs} ms ≥ {Percent(MeasurementTargets.MinOnsetAccuracy, "0")}: {Verdict(total.MeetsOnsetAccuracy)}"));
        text.AppendLine(Invariant($"- Analystid: {MicrosecondsPerBuffer:0.0} µs per buffert i snitt ({Buffers} buffertar, {AudioSeconds:0.0} s ljud på {AnalysisTime.TotalSeconds:0.00} s)"));
        return text.ToString();
    }

    private void AppendRows(StringBuilder text, Func<string, ScoreCounts, string> row)
    {
        foreach (ClipScore clip in Score.Clips)
        {
            text.AppendLine(row(clip.Clip, clip.Counts));
        }

        text.AppendLine(row("**Totalt**", Score.Total));
    }

    private static string PitchRow(string name, ScoreCounts counts)
    {
        return $"| {name} | {counts.VoicedWindows} | {Percent(counts.PitchAccuracy)} ({counts.CorrectWindows}) | "
            + $"{Percent(counts.OctaveErrors)} ({counts.OctaveErrorWindows}) | {counts.UnvoicedWindows} | "
            + $"{Percent(counts.FalseNotes)} ({counts.FalseNoteWindows}) |";
    }

    private static string LevelRow(string name, ScoreCounts counts)
    {
        return $"| {name} | {counts.SilenceOrBreathWindows} | {counts.NotesInSilenceOrBreath} | {Percent(Share(counts.NotesCutByGate, counts.CorrectWindows))} ({counts.NotesCutByGate}) | {counts.ReferenceOnsets} | "
            + $"{Percent(counts.OnsetAccuracy)} ({counts.MatchedOnsets}) | {counts.ExtraOnsets} ({counts.ExtraOnsetsInSilenceOrBreath}) |";
    }

    // Fixed formatting, so the report reads the same on every machine whatever its culture.
    private static string Percent(double? share, string format = "0.00")
    {
        return share is double value ? (value * 100).ToString(format, CultureInfo.InvariantCulture) + " %" : "–";
    }

    private static double? Share(int part, int whole)
    {
        return whole == 0 ? null : (double)part / whole;
    }

    private static string Verdict(bool met)
    {
        return met ? "uppnått" : "**inte uppnått**";
    }
}
