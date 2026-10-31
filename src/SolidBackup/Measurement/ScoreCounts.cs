namespace SolidBackup.Measurement;

/// <summary>
/// The window and onset counts behind the measures, for one clip or summed over many. Unvoiced windows
/// include noise; the windows in silence and breath alone are also counted, with the notes a detector
/// gives there while its gate is open, since a gated detector must give no note there at all; and, as the
/// other side of that trade-off, the voiced windows where the detector found the right note but the gate
/// was closed. Extra onsets that fall in silence or breath are counted too.
/// </summary>
public sealed record ScoreCounts(
    int VoicedWindows,
    int CorrectWindows,
    int OctaveErrorWindows,
    int UnvoicedWindows,
    int FalseNoteWindows,
    int SilenceOrBreathWindows,
    int NotesInSilenceOrBreath,
    int NotesCutByGate,
    int ReferenceOnsets,
    int MatchedOnsets,
    int ExtraOnsets,
    int ExtraOnsetsInSilenceOrBreath)
{
    public static ScoreCounts Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Share of voiced windows within ±50 cents; null when there are none.</summary>
    public double? PitchAccuracy => Share(CorrectWindows, VoicedWindows);

    public double? OctaveErrors => Share(OctaveErrorWindows, VoicedWindows);

    public double? FalseNotes => Share(FalseNoteWindows, UnvoicedWindows);

    public double? OnsetAccuracy => Share(MatchedOnsets, ReferenceOnsets);

    public bool MeetsPitchAccuracy => PitchAccuracy >= MeasurementTargets.MinPitchAccuracy;

    public bool MeetsOctaveErrors => OctaveErrors <= MeasurementTargets.MaxOctaveErrors;

    public bool MeetsFalseNotes => FalseNotes <= MeasurementTargets.MaxFalseNotes;

    public bool MeetsOnsetAccuracy => OnsetAccuracy >= MeasurementTargets.MinOnsetAccuracy;

    public bool MeetsNoNotesInSilenceOrBreath => NotesInSilenceOrBreath <= MeasurementTargets.MaxNotesInSilenceOrBreath;

    public static ScoreCounts operator +(ScoreCounts left, ScoreCounts right)
    {
        return new ScoreCounts(
            left.VoicedWindows + right.VoicedWindows,
            left.CorrectWindows + right.CorrectWindows,
            left.OctaveErrorWindows + right.OctaveErrorWindows,
            left.UnvoicedWindows + right.UnvoicedWindows,
            left.FalseNoteWindows + right.FalseNoteWindows,
            left.SilenceOrBreathWindows + right.SilenceOrBreathWindows,
            left.NotesInSilenceOrBreath + right.NotesInSilenceOrBreath,
            left.NotesCutByGate + right.NotesCutByGate,
            left.ReferenceOnsets + right.ReferenceOnsets,
            left.MatchedOnsets + right.MatchedOnsets,
            left.ExtraOnsets + right.ExtraOnsets,
            left.ExtraOnsetsInSilenceOrBreath + right.ExtraOnsetsInSilenceOrBreath);
    }

    private static double? Share(int part, int whole)
    {
        return whole == 0 ? null : (double)part / whole;
    }
}
