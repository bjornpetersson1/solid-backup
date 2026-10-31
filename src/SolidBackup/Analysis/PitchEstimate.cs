using System.Globalization;

using SolidBackup.Configuration;

namespace SolidBackup.Analysis;

/// <summary>
/// The pitch detector's answer for one buffer: no note, or a note with its frequency, its pitch in MIDI
/// cents (A4 = 6900), the nearest equal-tempered note and the deviation from it, and a confidence from 0 to 1.
/// <see cref="TimeMs"/> is the centre of the analysis window, counted from the detector's first sample.
/// </summary>
public readonly record struct PitchEstimate(double TimeMs, bool HasNote, double MidiCents, double Confidence)
{
    public static PitchEstimate NoNote(double timeMs, double confidence)
    {
        return new PitchEstimate(timeMs, false, 0, confidence);
    }

    /// <summary>The frequency in Hz; 0 for no note.</summary>
    public double Frequency => HasNote ? Pitch.Frequency(MidiCents) : 0;

    /// <summary>The nearest MIDI note, e.g. 69 for A4.</summary>
    public int MidiNote => (int)Math.Round(MidiCents / 100, MidpointRounding.AwayFromZero);

    /// <summary>The deviation from <see cref="MidiNote"/> in cents, from −50 to +50.</summary>
    public double Cents => MidiCents - (MidiNote * 100.0);

    /// <summary>The note and deviation for display, e.g. "A4 +12 cent", or "–" for no note.</summary>
    public override string ToString()
    {
        return HasNote
            ? string.Create(CultureInfo.InvariantCulture, $"{NoteName.Format(MidiNote)} {Cents:+0;-0;0} cent")
            : "–";
    }
}
