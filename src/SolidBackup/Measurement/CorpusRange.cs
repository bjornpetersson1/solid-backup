namespace SolidBackup.Measurement;

/// <summary>The pitch range the corpus is measured in: the default E2–C7 from <c>appsettings.json</c>.</summary>
public static class CorpusRange
{
    public const int LowestMidi = 40;
    public const int HighestMidi = 96;

    public static bool Contains(int midi)
    {
        return midi is >= LowestMidi and <= HighestMidi;
    }

    /// <summary>Whether a pitch's nearest note lies in the range; a pitch exactly halfway counts as the upper note, as in <c>PitchEstimate.MidiNote</c>.</summary>
    public static bool ContainsPitch(double midiCents)
    {
        return Contains((int)Math.Round(midiCents / 100, MidpointRounding.AwayFromZero));
    }
}
