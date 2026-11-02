using SolidBackup.Analysis;
using SolidBackup.Harmony;

namespace SolidBackup.Live;

/// <summary>
/// What live mode heard and decided for the last input buffer: the pitch, the level, the gate, the chord that sounds
/// (none while nothing sounds) and the current key (none for a style without one, or before the first).
/// </summary>
public readonly record struct LiveState(PitchEstimate Estimate, double LevelDbfs, bool GateOpen, Chord? Chord, Key? Key);
