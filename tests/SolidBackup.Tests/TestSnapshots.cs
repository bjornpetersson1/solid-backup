using SolidBackup.Analysis;
using SolidBackup.Audio;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Live;

namespace SolidBackup.Tests;

/// <summary>Live snapshots for the dashboard and summary tests; vary them with <c>with</c>.</summary>
internal static class TestSnapshots
{
    /// <summary>A4 +12 cent at −24 dBFS with the gate open, Am in C major, 42.8 ms in all, defaults in C Ionian with Arpeggio.</summary>
    public static readonly LiveSnapshot Playing = new(
        new LiveState(new PitchEstimate(0, HasNote: true, 6912, Confidence: 1), -24, GateOpen: true, new Chord(9, ChordQuality.Minor), new Key(0, Mode.Ionian)),
        TestSettings.Default with { Key = "C", Mode = "Ionian", RhythmPattern = "Arpeggio" },
        new LiveLatency(6, 30.5, 1.3333, 5),
        default,
        Recording: null);

    /// <summary>The live state only, as live mode reads it, with fixed latencies and no counts.</summary>
    public static LiveSnapshot Of(LiveState state, AppSettings settings)
    {
        return Playing with { State = state, Settings = settings, Latency = new LiveLatency(6, 34, 1.3, 5) };
    }
}
