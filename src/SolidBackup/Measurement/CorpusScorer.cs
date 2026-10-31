using SolidBackup.Audio;

namespace SolidBackup.Measurement;

/// <summary>
/// Runs a detector over corpus clips buffer by buffer, through <see cref="IAudioInput"/>, and scores
/// its reports against the references by the rules in <see cref="MeasurementTargets"/>.
/// </summary>
public static class CorpusScorer
{
    /// <summary>Scores the clips with one fresh detector per clip, keeping them in their given order.</summary>
    /// <param name="createDetector">Makes a fresh detector for a clip; with <paramref name="parallel"/> it is called from several threads.</param>
    /// <param name="parallel">Runs the clips on several threads; leave it off when timing a detector.</param>
    public static CorpusScore Score(IEnumerable<CorpusClip> clips, Func<CorpusClip, ICorpusDetector> createDetector, int bufferSize, bool parallel = false)
    {
        Func<CorpusClip, ClipScore> score = clip => new ClipScore(clip.Name, Score(clip, Run(clip, createDetector(clip), bufferSize)));
        return new CorpusScore(parallel ? clips.AsParallel().AsOrdered().Select(score).ToArray() : clips.Select(score).ToArray());
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
        int notesCutByGate = 0;
        foreach (DetectorReport report in reports)
        {
            if (reference.SegmentAt(report.TimeMs) is not ReferenceSegment segment || NearBoundary(segment, report.TimeMs))
            {
                continue;
            }

            if (segment.IsVoiced)
            {
                // A note can drift past the range's edge; windows whose reference lies nearer a note outside it are out of range too.
                double? expected = segment.PitchAt(report.TimeMs);
                if (expected is double pitch && !CorpusRange.ContainsPitch(pitch))
                {
                    continue;
                }

                voiced++;
                if (report.MidiCents - expected is double error)
                {
                    bool right = Math.Abs(error) <= MeasurementTargets.PitchToleranceCents;
                    correct += right ? 1 : 0;
                    notesCutByGate += right && !report.GateOpen ? 1 : 0;
                    octave += Math.Abs(error) >= MeasurementTargets.OctaveErrorCents ? 1 : 0;
                }
            }
            else if (segment.IsUnvoiced)
            {
                unvoiced++;
                falseNotes += report.MidiCents.HasValue ? 1 : 0;
                if (IsSilenceOrBreath(segment))
                {
                    silenceOrBreath++;
                    notesInSilenceOrBreath += report.MidiCents.HasValue && report.GateOpen ? 1 : 0;
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
            NotesCutByGate = notesCutByGate,
        };
    }

    // Matches reference and detected onsets one to one, in time order: each reference onset takes the
    // earliest unmatched detected onset within the tolerance, which maximises the matches.
    private static ScoreCounts ScoreOnsets(ClipReference reference, IReadOnlyList<DetectorReport> reports)
    {
        double[] detected = reports.Select(report => report.OnsetMs).OfType<double>().Order().ToArray();
        double[] expected = reference.OnsetsMs.Order().ToArray();
        bool[] matched = new bool[detected.Length];
        int next = 0;
        foreach (double onset in expected)
        {
            while (next < detected.Length && detected[next] < onset - MeasurementTargets.OnsetToleranceMs)
            {
                next++;
            }

            if (next < detected.Length && detected[next] <= onset + MeasurementTargets.OnsetToleranceMs)
            {
                matched[next] = true;
                next++;
            }
        }

        int matches = 0;
        int extrasInSilenceOrBreath = 0;
        for (int i = 0; i < detected.Length; i++)
        {
            if (matched[i])
            {
                matches++;
            }
            else if (reference.SegmentAt(detected[i]) is { } segment && IsSilenceOrBreath(segment))
            {
                extrasInSilenceOrBreath++;
            }
        }

        return ScoreCounts.Zero with
        {
            ReferenceOnsets = expected.Length,
            MatchedOnsets = matches,
            ExtraOnsets = detected.Length - matches,
            ExtraOnsetsInSilenceOrBreath = extrasInSilenceOrBreath,
        };
    }

    private static bool IsSilenceOrBreath(ReferenceSegment segment)
    {
        return segment.Kind is SegmentKind.Silence or SegmentKind.Breath;
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
