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

    /// <summary>The nearest MIDI note; a pitch exactly halfway counts as the upper note.</summary>
    public static int NearestMidi(double midiCents)
    {
        return (int)Math.Round(midiCents / 100, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Where a parabola through three equally spaced values has its extreme, as an offset from the middle one
    /// (−½ to +½); 0 when the middle value is not a minimum (or, with <paramref name="minimum"/> false, a maximum).
    /// </summary>
    public static double ParabolaOffset(double left, double centre, double right, bool minimum)
    {
        double curvature = left - (2 * centre) + right;
        bool extreme = minimum ? curvature > 0 : curvature < 0;
        return extreme ? Math.Clamp(0.5 * (left - right) / curvature, -0.5, 0.5) : 0;
    }
}
