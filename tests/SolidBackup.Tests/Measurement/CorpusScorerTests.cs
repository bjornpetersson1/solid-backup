using SolidBackup.Measurement;

namespace SolidBackup.Tests.Measurement;

public sealed class CorpusScorerTests
{
    // Silence 0–100 ms, A4 100–1100 ms, silence 1100–2000 ms, one onset at 100 ms. With a report every
    // 10 ms at 5, 15, … ms and 30 ms left out at every boundary, that is 4 + 84 = 88 unvoiced windows
    // and 94 voiced windows (135 to 1065 ms).
    private static readonly CorpusClip Constructed = new(
        "constructed",
        48_000,
        new float[96_000],
        new ClipReference(
            [
                new ReferenceSegment(0, 100, SegmentKind.Silence),
                new ReferenceSegment(100, 1100, SegmentKind.Note, [6900]),
                new ReferenceSegment(1100, 2000, SegmentKind.Silence),
            ],
            [100]));

    [Fact]
    public void OracleDetector_ScoresPerfectlyOnTheGeneratedCorpus()
    {
        CorpusScore score = CorpusScorer.Score(TestCorpus.Generated, clip => new OracleDetector(clip.Reference, clip.SampleRate), bufferSize: 64);

        AssertPerfect(score.Total);
        Assert.Equal(TestCorpus.Generated.Count, score.Clips.Count);
        Assert.True(score.Total.VoicedWindows > 30_000);
        Assert.True(score.Total.UnvoicedWindows > 5_000);
        Assert.Equal((4 * (CorpusRange.HighestMidi - CorpusRange.LowestMidi + 1)) + 2, score.Total.ReferenceOnsets);
    }

    [Fact]
    public void ExactReports_ScorePerfectly_WithTheExpectedWindowCounts()
    {
        ScoreCounts counts = CorpusScorer.Score(Constructed, Reports(time => time is >= 100 and < 1100 ? 6900 : null, onsets: [100]));

        Assert.Equal((94, 94, 0), (counts.VoicedWindows, counts.CorrectWindows, counts.OctaveErrorWindows));
        Assert.Equal((88, 0), (counts.UnvoicedWindows, counts.FalseNoteWindows));
        Assert.Equal((88, 0), (counts.SilenceOrBreathWindows, counts.NotesInSilenceOrBreath));
        Assert.Equal((1, 1, 0), (counts.ReferenceOnsets, counts.MatchedOnsets, counts.ExtraOnsets));
        AssertPerfect(counts);
    }

    [Fact]
    public void ConstructedErrors_GiveExactlyTheExpectedFigures()
    {
        // In the note: 10 windows an octave up, 5 with no note, 4 that are 60 cents sharp. In the
        // silence after it: 3 windows with a note.
        static double? Pitch(double time) => time switch
        {
            >= 200 and < 300 => 8100,
            >= 300 and < 350 => null,
            >= 400 and < 440 => 6960,
            >= 1500 and < 1530 => 6900,
            >= 100 and < 1100 => 6900,
            _ => null,
        };

        ScoreCounts counts = CorpusScorer.Score(Constructed, Reports(Pitch, onsets: [125]));

        Assert.Equal((94, 75, 10), (counts.VoicedWindows, counts.CorrectWindows, counts.OctaveErrorWindows));
        Assert.Equal((88, 3), (counts.UnvoicedWindows, counts.FalseNoteWindows));
        Assert.Equal((88, 3), (counts.SilenceOrBreathWindows, counts.NotesInSilenceOrBreath));
        Assert.Equal(75.0 / 94, counts.PitchAccuracy);
        Assert.Equal(10.0 / 94, counts.OctaveErrors);
        Assert.Equal(3.0 / 88, counts.FalseNotes);
        Assert.Equal((1, 0, 1), (counts.ReferenceOnsets, counts.MatchedOnsets, counts.ExtraOnsets));
        Assert.Equal(0, counts.OnsetAccuracy);
        Assert.False(counts.MeetsPitchAccuracy || counts.MeetsOctaveErrors || counts.MeetsFalseNotes || counts.MeetsOnsetAccuracy);
    }

    [Theory]
    [InlineData(80, 1)]
    [InlineData(120, 1)]
    [InlineData(79.9, 0)]
    [InlineData(120.1, 0)]
    public void Onset_MatchesWithin20Ms(double detected, int matched)
    {
        ScoreCounts counts = CorpusScorer.Score(Constructed, Reports(_ => null, onsets: [detected]));

        Assert.Equal(matched, counts.MatchedOnsets);
        Assert.Equal(1 - matched, counts.ExtraOnsets);
    }

    [Fact]
    public void TwoDetectedOnsetsNearOneReference_MatchOnlyOnce()
    {
        ScoreCounts counts = CorpusScorer.Score(Constructed, Reports(_ => null, onsets: [95, 105]));

        Assert.Equal((1, 1), (counts.MatchedOnsets, counts.ExtraOnsets));
    }

    [Fact]
    public void OutOfRangeSegments_AreLeftOutOfEveryMeasure()
    {
        CorpusClip clip = Constructed with
        {
            Reference = new ClipReference([new ReferenceSegment(0, 2000, SegmentKind.OutOfRange, [9000])], []),
        };

        ScoreCounts counts = CorpusScorer.Score(clip, Reports(_ => 1234, onsets: []));

        Assert.Equal(ScoreCounts.Zero, counts);
        Assert.Null(counts.PitchAccuracy);
    }

    [Fact]
    public void NotesInNoise_AreFalseNotesButNotNotesInSilenceOrBreath()
    {
        CorpusClip clip = Constructed with
        {
            Reference = new ClipReference(
                [
                    new ReferenceSegment(0, 1000, SegmentKind.Noise),
                    new ReferenceSegment(1000, 1500, SegmentKind.Breath),
                    new ReferenceSegment(1500, 2000, SegmentKind.Silence),
                ],
                []),
        };

        // Notes everywhere: 94 noise windows, 44 breath and 44 silence windows away from the boundaries.
        ScoreCounts counts = CorpusScorer.Score(clip, Reports(_ => 6900, onsets: []));

        Assert.Equal((182, 182), (counts.UnvoicedWindows, counts.FalseNoteWindows));
        Assert.Equal((88, 88), (counts.SilenceOrBreathWindows, counts.NotesInSilenceOrBreath));
    }

    [Fact]
    public void VoicedWindowsWhoseReferenceDriftsPastTheRange_AreLeftOut()
    {
        // The note glides from C7 (9600) to D7 (9800); past 9650 its windows are out of range.
        CorpusClip clip = Constructed with
        {
            Reference = new ClipReference([new ReferenceSegment(0, 2000, SegmentKind.Note, [9600, 9800])], []),
        };

        ScoreCounts counts = CorpusScorer.Score(clip, Reports(_ => null, onsets: []));

        // 9650 is reached at 500 ms; windows from 35 to 495 ms count (47), all missed.
        Assert.Equal((47, 0), (counts.VoicedWindows, counts.CorrectWindows));
    }

    [Fact]
    public void NotesInSilenceWhileTheGateIsClosed_DoNotCount()
    {
        // Notes everywhere, the gate open only before 1500 ms: of the 84 windows in the closing silence,
        // those at 1135–1495 ms (37) are notes with the gate open.
        DetectorReport[] reports = Reports(_ => 6900, onsets: []).Select(report => report with { GateOpen = report.TimeMs < 1500 }).ToArray();

        ScoreCounts counts = CorpusScorer.Score(Constructed, reports);

        Assert.Equal(88, counts.FalseNoteWindows);
        Assert.Equal((88, 4 + 37), (counts.SilenceOrBreathWindows, counts.NotesInSilenceOrBreath));
        Assert.False(counts.MeetsNoNotesInSilenceOrBreath);
    }

    [Fact]
    public void RightNotesWhileTheGateIsClosed_AreCountedAsCutOff()
    {
        // The note is right throughout; the gate is closed from 600 ms, so 47 of its 94 windows are cut off.
        DetectorReport[] reports = Reports(time => time is >= 100 and < 1100 ? 6900 : null, onsets: [])
            .Select(report => report with { GateOpen = report.TimeMs < 600 })
            .ToArray();

        ScoreCounts counts = CorpusScorer.Score(Constructed, reports);

        Assert.Equal((94, 47), (counts.CorrectWindows, counts.NotesCutByGate));
    }

    [Fact]
    public void ExtraOnsetsInSilence_AreCountedApart()
    {
        // 100 matches the reference; 600 lies in the note, 1500 in the silence after it.
        ScoreCounts counts = CorpusScorer.Score(Constructed, Reports(_ => null, onsets: [100, 600, 1500]));

        Assert.Equal((1, 2, 1), (counts.MatchedOnsets, counts.ExtraOnsets, counts.ExtraOnsetsInSilenceOrBreath));
    }

    [Fact]
    public void UnclearSegments_AreLeftOutOfEveryMeasure()
    {
        CorpusClip clip = Constructed with
        {
            Reference = new ClipReference([new ReferenceSegment(0, 2000, SegmentKind.Unclear)], []),
        };

        Assert.Equal(ScoreCounts.Zero, CorpusScorer.Score(clip, Reports(_ => 6900, onsets: [])));
    }

    [Fact]
    public void Total_SumsTheClips()
    {
        ScoreCounts one = ScoreCounts.Zero with { VoicedWindows = 10, CorrectWindows = 9, ReferenceOnsets = 2, MatchedOnsets = 2 };
        ScoreCounts two = ScoreCounts.Zero with { VoicedWindows = 10, CorrectWindows = 10, UnvoicedWindows = 4, FalseNoteWindows = 1 };

        ScoreCounts total = new CorpusScore([new ClipScore("one", one), new ClipScore("two", two)]).Total;

        Assert.Equal(0.95, total.PitchAccuracy);
        Assert.True(total.MeetsPitchAccuracy);
        Assert.Equal(0.25, total.FalseNotes);
        Assert.False(total.MeetsFalseNotes);
    }

    private static void AssertPerfect(ScoreCounts counts)
    {
        Assert.Equal(1, counts.PitchAccuracy);
        Assert.Equal(0, counts.OctaveErrors);
        Assert.Equal(0, counts.FalseNotes);
        Assert.Equal(1, counts.OnsetAccuracy);
        Assert.Equal(0, counts.ExtraOnsets);
        Assert.True(counts.MeetsPitchAccuracy && counts.MeetsOctaveErrors && counts.MeetsFalseNotes && counts.MeetsOnsetAccuracy);
    }

    private static DetectorReport[] Reports(Func<double, double?> pitch, double[] onsets)
    {
        DetectorReport[] reports = Enumerable.Range(0, 200).Select(i => new DetectorReport((i * 10) + 5, pitch((i * 10) + 5))).ToArray();
        for (int i = 0; i < onsets.Length; i++)
        {
            reports[i] = reports[i] with { OnsetMs = onsets[i] };
        }

        return reports;
    }
}
