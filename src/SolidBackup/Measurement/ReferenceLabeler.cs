namespace SolidBackup.Measurement;

/// <summary>
/// Drafts the reference for a recording from its note list. Notes are found as loud, periodic stretches
/// of the level envelope, one per listed note; each note's pitch curve is measured offline every 10 ms by
/// searching only within a semitone of the listed note, so the label cannot be an octave off. The gaps
/// between notes are silence or breath, and each note's onset is where its level rise begins.
/// </summary>
public static class ReferenceLabeler
{
    private const double OnsetBelowPeakDb = 20;
    private const double BreathAboveFloorDb = 10;

    public static ClipReference Label(float[] samples, int sampleRate, ScriptedClip clip)
    {
        if (!clip.IsPitched)
        {
            SegmentKind kind = clip.Kind == RecordingKind.Breath ? SegmentKind.Breath : SegmentKind.Silence;
            return new ClipReference([new ReferenceSegment(0, samples.Length * 1000.0 / sampleRate, kind)], []);
        }

        RecordingAnalysis analysis = new(samples, sampleRate);
        List<(int Start, int End, double MedianCents)> regions = analysis.PitchedRegions();
        if (regions.Count != clip.Notes.Count)
        {
            throw new InvalidDataException(
                $"Klippet {clip.Name} har {regions.Count} toner i inspelningen men {clip.Notes.Count} i listan.");
        }

        List<ReferenceSegment> segments = [];
        List<double> onsets = [];
        double position = 0;
        int previousLoudEnd = 0;
        for (int i = 0; i < regions.Count; i++)
        {
            (int start, int end, _) = regions[i];
            double nextStart = i + 1 < regions.Count ? regions[i + 1].Start : analysis.DurationMs;
            (double soundStart, double soundEnd) = PeriodicSpan(analysis, clip.Notes[i], position, start, end, nextStart);
            AddGap(segments, analysis, position, soundStart);
            AddNote(segments, analysis, soundStart, start, end, soundEnd, clip.Notes[i], clip.Name);
            if (CorpusRange.Contains(clip.Notes[i]))
            {
                // The onset may lie in the previous note's quiet tail, so the search stops at its loud stretch.
                onsets.Add(Onset(analysis, previousLoudEnd, start, end));
            }

            position = soundEnd;
            previousLoudEnd = end;
        }

        AddGap(segments, analysis, position, analysis.DurationMs);
        return new ClipReference(segments, onsets);
    }

    /// <summary>
    /// Proposes a note list for a recording: one note per loud, periodic stretch, the nearest note to its
    /// median pitch, searched over the whole corpus range. The proposal must be checked by ear before use.
    /// </summary>
    public static IReadOnlyList<int> SuggestNotes(float[] samples, int sampleRate)
    {
        return new RecordingAnalysis(samples, sampleRate).PitchedRegions()
            .Select(region => (int)Math.Round(region.MedianCents / 100))
            .ToArray();
    }

    // A note's quiet lead-in and tail, below the loud stretch's level but still periodic near the note, are
    // part of its sound: the span grows from the loud stretch in 10 ms steps while the frames stay periodic.
    private static (double Start, double End) PeriodicSpan(RecordingAnalysis analysis, int midi, double previousEnd, int start, int end, double nextStart)
    {
        double lowest = (midi - 1) * 100.0;
        double highest = (midi + 1) * 100.0;
        const double Step = RecordingAnalysis.HopMs;

        // Frames are Step apart; a gap larger than that ends the run.
        List<(double TimeMs, double Cents)> after = analysis.PitchFrames(end, nextStart, Step, lowest, highest, RecordingAnalysis.LagChoice.Best);
        double soundEnd = end;
        int next = 0;
        while (next < after.Count && after[next].TimeMs <= soundEnd + 0.001)
        {
            soundEnd = Math.Min(nextStart, after[next].TimeMs + Step);
            next++;
        }

        List<(double TimeMs, double Cents)> before = analysis.PitchFrames(previousEnd, start, Step, lowest, highest, RecordingAnalysis.LagChoice.Best);
        double soundStart = start;
        int previous = before.Count - 1;
        while (previous >= 0 && before[previous].TimeMs >= soundStart - Step - 0.001)
        {
            soundStart = Math.Max(previousEnd, before[previous].TimeMs);
            previous--;
        }

        return (soundStart, soundEnd);
    }

    // The note runs from its first to its last periodic frame within the loud stretch; the rest of the note's
    // sound (an attack, a decay, a quiet tail) is unclear. Its pitch curve has one value per 10 ms, frames not
    // periodic enough filled in from their neighbours.
    private static void AddNote(List<ReferenceSegment> segments, RecordingAnalysis analysis, double soundStart, int start, int end, double soundEnd, int midi, string clip)
    {
        List<(double TimeMs, double Cents)> voiced = analysis.PitchFrames(
            start, end, RecordingAnalysis.HopMs, (midi - 1) * 100.0, (midi + 1) * 100.0, RecordingAnalysis.LagChoice.Best);
        if (voiced.Count == 0)
        {
            throw new InvalidDataException($"Klippet {clip} har en ton vid {start} ms utan mätbar tonhöjd nära listans ton.");
        }

        double first = voiced[0].TimeMs;
        double last = voiced[^1].TimeMs;
        double[] pitches = Resample(voiced, first, (int)Math.Round((last - first) / RecordingAnalysis.HopMs) + 1);
        double noteEnd = pitches.Length > 1 ? last : Math.Min(end, first + RecordingAnalysis.HopMs);
        if (first > soundStart)
        {
            segments.Add(new ReferenceSegment(soundStart, first, SegmentKind.Unclear));
        }

        segments.Add(new ReferenceSegment(first, noteEnd, CorpusRange.Contains(midi) ? SegmentKind.Note : SegmentKind.OutOfRange, pitches));
        if (noteEnd < soundEnd)
        {
            segments.Add(new ReferenceSegment(noteEnd, soundEnd, SegmentKind.Unclear));
        }
    }

    private static double[] Resample(List<(double TimeMs, double Cents)> voiced, double first, int count)
    {
        double[] pitches = new double[count];
        int next = 0;
        for (int i = 0; i < count; i++)
        {
            double time = first + (i * RecordingAnalysis.HopMs);
            while (voiced[next].TimeMs < time - 0.001)
            {
                next++;
            }

            (double beforeTime, double beforeCents) = voiced[Math.Max(0, next - 1)];
            (double afterTime, double afterCents) = voiced[next];
            pitches[i] = afterTime <= beforeTime || time >= afterTime
                ? afterCents
                : beforeCents + ((afterCents - beforeCents) * (time - beforeTime) / (afterTime - beforeTime));
        }

        return pitches;
    }

    // Where the level rise begins: from the stretch's start, back towards the previous note while the level
    // stays within 20 dB of the note's peak and above the midpoint between the floor and that peak.
    private static double Onset(RecordingAnalysis analysis, int previousEnd, int start, int end)
    {
        double[] level = analysis.Level;
        double peak = level.AsSpan(start, end - start).ToArray().Max();
        double threshold = Math.Max(peak - OnsetBelowPeakDb, (analysis.Floor + peak) / 2);
        int onset = start;
        while (onset > previousEnd && level[onset - 1] >= threshold)
        {
            onset--;
        }

        while (onset < end && level[onset] < threshold)
        {
            onset++;
        }

        return onset;
    }

    private static void AddGap(List<ReferenceSegment> segments, RecordingAnalysis analysis, double startMs, double endMs)
    {
        if (endMs <= startMs)
        {
            return;
        }

        int from = (int)startMs;
        int to = Math.Min(analysis.Level.Length, (int)endMs);
        bool breath = to > from && RecordingAnalysis.Median(analysis.Level[from..to]) > analysis.Floor + BreathAboveFloorDb;
        segments.Add(new ReferenceSegment(startMs, endMs, breath ? SegmentKind.Breath : SegmentKind.Silence));
    }
}
