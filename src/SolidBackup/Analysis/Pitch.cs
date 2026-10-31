namespace SolidBackup.Analysis;

/// <summary>Converts between frequency and pitch in MIDI cents, equal-tempered at A4 = 440 Hz = 6900 cents.</summary>
public static class Pitch
{
    public static double Frequency(double midiCents)
    {
        return 440 * Math.Pow(2, (midiCents - 6900) / 1200);
    }

    public static double Cents(double frequency)
    {
        return 6900 + (1200 * Math.Log2(frequency / 440));
    }
}
