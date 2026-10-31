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

    public static double Frequency(double midiCents)
    {
        return 440 * Math.Pow(2, (midiCents - 6900) / 1200);
    }

    public static double Cents(double frequency)
    {
        return 6900 + (1200 * Math.Log2(frequency / 440));
    }
}
