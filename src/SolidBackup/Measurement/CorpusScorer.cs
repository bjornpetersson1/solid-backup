using SolidBackup.Audio;

namespace SolidBackup.Measurement;

/// <summary>
/// Runs a detector over corpus clips buffer by buffer, through <see cref="IAudioInput"/>, and scores
/// its reports against the references by the rules in <see cref="MeasurementTargets"/>.
/// </summary>
public static class CorpusScorer
{
    /// <param name="createDetector">Makes a fresh detector for a clip, given the clip and its sample rate.</param>
    public static CorpusScore Score(IEnumerable<CorpusClip> clips, Func<CorpusClip, ICorpusDetector> createDetector, int bufferSize)
    {
        return new CorpusScore(clips.Select(clip => new ClipScore(clip.Name, Score(clip, Run(clip, createDetector(clip), bufferSize)))).ToArray());
    }

    public static ScoreCounts Score(CorpusClip clip, IReadOnlyList<DetectorReport> reports)
    {
        return ScoreWindows(clip.Reference, reports) + ScoreOnsets(clip.Reference, reports);
    }

    private static List<DetectorReport> Run(CorpusClip clip, ICorpusDetector detector, int bufferSize)
    {
        Collector collector = new(detector);
        using IAudioInput input = clip.OpenInput(bufferSize);
        input.Start(collector);
        return collector.Reports;
    }

    private static ScoreCounts ScoreWindows(ClipReference reference, IReadOnlyList<DetectorReport> reports)
    {
        int voiced = 0;
        int correct = 0;
        int octave = 0;
        int unvoiced = 0;
        int falseNotes = 0;
        int silenceOrBreath = 0;
        int notesInSilenceOrBreath = 0;
        foreach (DetectorReport report in reports)
        {
            if (reference.SegmentAt(report.TimeMs) is not ReferenceSegment segment || NearBoundary(segment, report.TimeMs))
            {
                continue;
            }

            if (segment.IsVoiced)
            {
                voiced++;
                if (report.MidiCents - segment.PitchAt(report.TimeMs) is double error)
                {
                    correct += Math.Abs(error) <= MeasurementTargets.PitchToleranceCents ? 1 : 0;
                    octave += Math.Abs(error) >= MeasurementTargets.OctaveErrorCents ? 1 : 0;
                }
            }
            else if (segment.IsUnvoiced)
            {
                int note = report.MidiCents.HasValue ? 1 : 0;
                unvoiced++;
                falseNotes += note;
                if (segment.Kind is SegmentKind.Silence or SegmentKind.Breath)
                {
                    silenceOrBreath++;
                    notesInSilenceOrBreath += note;
                }
            }
        }

        return ScoreCounts.Zero with
        {
            VoicedWindows = voiced,
            CorrectWindows = correct,
            OctaveErrorWindows = octave,
            UnvoicedWindows = unvoiced,
            FalseNoteWindows = falseNotes,
            SilenceOrBreathWindows = silenceOrBreath,
            NotesInSilenceOrBreath = notesInSilenceOrBreath,
        };
    }

    // Matches reference and detected onsets one to one, in time order: each reference onset takes the
    // earliest unmatched detected onset within the tolerance, which maximises the matches.
    private static ScoreCounts ScoreOnsets(ClipReference reference, IReadOnlyList<DetectorReport> reports)
    {
        double[] detected = reports.Select(report => report.OnsetMs).OfType<double>().Order().ToArray();
        double[] expected = reference.OnsetsMs.Order().ToArray();
        int next = 0;
        int matched = 0;
        foreach (double onset in expected)
        {
            while (next < detected.Length && detected[next] < onset - MeasurementTargets.OnsetToleranceMs)
            {
                next++;
            }

            if (next < detected.Length && detected[next] <= onset + MeasurementTargets.OnsetToleranceMs)
            {
                matched++;
                next++;
            }
        }

        return ScoreCounts.Zero with
        {
            ReferenceOnsets = expected.Length,
            MatchedOnsets = matched,
            ExtraOnsets = detected.Length - matched,
        };
    }

    private static bool NearBoundary(ReferenceSegment segment, double timeMs)
    {
        return timeMs - segment.StartMs < MeasurementTargets.BoundaryExclusionMs
            || segment.EndMs - timeMs < MeasurementTargets.BoundaryExclusionMs;
    }

    private sealed class Collector(ICorpusDetector detector) : IAudioInputHandler
    {
        public List<DetectorReport> Reports { get; } = [];

        public void OnBuffer(ReadOnlySpan<float> samples)
        {
            Reports.Add(detector.Process(samples));
        }
    }
}
