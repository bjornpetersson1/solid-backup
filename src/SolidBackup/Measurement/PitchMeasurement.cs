using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

using SolidBackup.Analysis;
using SolidBackup.Configuration;

using static System.FormattableString;

namespace SolidBackup.Measurement;

/// <summary>The pitch detector scored on corpus clips, with the analysis time it took.</summary>
public sealed record PitchMeasurement(CorpusScore Score, long Buffers, TimeSpan AnalysisTime, double AudioSeconds)
{
    public double MicrosecondsPerBuffer => Buffers == 0 ? 0 : AnalysisTime.TotalMicroseconds / Buffers;

    /// <summary>
    /// Runs a fresh detector made from <paramref name="settings"/> over each clip, at the clip's sample rate.
    /// In parallel the clips finish sooner, but the time per buffer is inflated by the threads sharing the CPU.
    /// </summary>
    public static PitchMeasurement Run(IEnumerable<CorpusClip> clips, AppSettings settings, bool parallel = false)
    {
        ConcurrentBag<PitchCorpusDetector> detectors = [];
        CorpusClip[] all = clips.ToArray();
        CorpusScore score = CorpusScorer.Score(
            all,
            clip =>
            {
                PitchCorpusDetector detector = new(PitchDetector.FromSettings(settings with { SampleRate = clip.SampleRate }));
                detectors.Add(detector);
                return detector;
            },
            settings.BufferSize,
            parallel);
        return new PitchMeasurement(
            score,
            detectors.Sum(detector => detector.Buffers),
            TimeSpan.FromTicks(detectors.Sum(detector => detector.AnalysisTime.Ticks)),
            all.Sum(clip => clip.Samples.Length / (double)clip.SampleRate));
    }

    /// <summary>The report as markdown: per clip and in total, each figure with its counts, and the targets.</summary>
    public string ToMarkdown()
    {
        StringBuilder text = new();
        text.AppendLine("| Klipp | Röstade fönster | Träffsäkerhet | Oktavfel | Tonlösa fönster | Falska toner |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (ClipScore clip in Score.Clips)
        {
            text.AppendLine(Row(clip.Clip, clip.Counts));
        }

        ScoreCounts total = Score.Total;
        text.AppendLine(Row("**Totalt**", total));
        text.AppendLine();
        text.AppendLine($"- Träffsäkerhet ≥ {Percent(MeasurementTargets.MinPitchAccuracy, "0")}: {Verdict(total.MeetsPitchAccuracy)}");
        text.AppendLine($"- Oktavfel ≤ {Percent(MeasurementTargets.MaxOctaveErrors, "0")}: {Verdict(total.MeetsOctaveErrors)}");
        text.AppendLine($"- Falska toner ≤ {Percent(MeasurementTargets.MaxFalseNotes, "0")}: {Verdict(total.MeetsFalseNotes)}");
        text.AppendLine(Invariant($"- Analystid: {MicrosecondsPerBuffer:0.0} µs per buffert i snitt ({Buffers} buffertar, {AudioSeconds:0.0} s ljud på {AnalysisTime.TotalSeconds:0.00} s)"));
        return text.ToString();
    }

    private static string Row(string name, ScoreCounts counts)
    {
        return $"| {name} | {counts.VoicedWindows} | {Percent(counts.PitchAccuracy)} ({counts.CorrectWindows}) | "
            + $"{Percent(counts.OctaveErrors)} ({counts.OctaveErrorWindows}) | {counts.UnvoicedWindows} | "
            + $"{Percent(counts.FalseNotes)} ({counts.FalseNoteWindows}) |";
    }

    // Fixed formatting, so the report reads the same on every machine whatever its culture.
    private static string Percent(double? share, string format = "0.00")
    {
        return share is double value ? (value * 100).ToString(format, CultureInfo.InvariantCulture) + " %" : "–";
    }

    private static string Verdict(bool met)
    {
        return met ? "uppnått" : "**inte uppnått**";
    }
}
