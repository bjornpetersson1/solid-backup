namespace SolidBackup.Measurement;

/// <summary>
/// What a detector says about one buffer: the time in the clip its estimate refers to, the pitch in
/// MIDI cents or null for no note, the time of an onset it found, if any, and whether its noise gate was
/// open. The gate only counts for the notes in silence and breath; the pitch measures take the pitch as it is.
/// </summary>
public readonly record struct DetectorReport(double TimeMs, double? MidiCents, double? OnsetMs = null, bool GateOpen = true);
