namespace SolidBackup.Measurement;

/// <summary>
/// What a detector says about one buffer: the time in the clip its estimate refers to, the pitch in
/// MIDI cents or null for no note, and the time of an onset it found, if any.
/// </summary>
public readonly record struct DetectorReport(double TimeMs, double? MidiCents, double? OnsetMs = null);
