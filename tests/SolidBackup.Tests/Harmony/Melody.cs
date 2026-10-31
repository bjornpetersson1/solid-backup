using SolidBackup.Analysis;
using SolidBackup.Configuration;
using SolidBackup.Harmony;

namespace SolidBackup.Tests.Harmony;

/// <summary>A note (or silence, with no name) from <see cref="StartMs"/> up to <see cref="EndMs"/>.</summary>
internal sealed record Note(string? Name, double StartMs, double EndMs);

/// <summary>Feeds melodies to a <see cref="Harmonizer"/> as pitch estimates at buffer intervals.</summary>
internal static class Melody
{
    public const double NoteMs = 500;

    /// <summary>Notes of <see cref="NoteMs"/> each, back to back from 0; "-" is a rest.</summary>
    public static Note[] Legato(params string[] names)
    {
        return [.. names.Select((name, i) => new Note(name == "-" ? null : name, i * NoteMs, (i + 1) * NoteMs))];
    }

    /// <summary>The estimates for the melody, one per buffer.</summary>
    public static PitchEstimate[] Estimates(Note[] notes, int sampleRate = 48_000, int bufferSize = 64)
    {
        List<PitchEstimate> estimates = [];
        for (int i = 0; ; i++)
        {
            double time = i * bufferSize * 1000.0 / sampleRate;
            if (time >= notes[^1].EndMs)
            {
                return [.. estimates];
            }

            Note note = notes.First(candidate => time >= candidate.StartMs && time < candidate.EndMs);
            estimates.Add(note.Name is null ? PitchEstimate.NoNote(time, 0) : new PitchEstimate(time, true, Midi(note.Name) * 100.0, 1));
        }
    }

    /// <summary>Every chord change, with the time of the estimate that made it.</summary>
    public static List<(double TimeMs, Chord Chord)> Changes(Harmonizer harmonizer, Note[] notes, int sampleRate = 48_000, int bufferSize = 64)
    {
        return Changes(harmonizer, Estimates(notes, sampleRate, bufferSize));
    }

    /// <summary>Every chord change over the estimates, with the time of the estimate that made it.</summary>
    public static List<(double TimeMs, Chord Chord)> Changes(Harmonizer harmonizer, PitchEstimate[] estimates)
    {
        List<(double, Chord)> changes = [];
        foreach (PitchEstimate estimate in estimates)
        {
            if (harmonizer.Process(estimate))
            {
                changes.Add((estimate.TimeMs, harmonizer.Chord ?? throw new InvalidOperationException("A change without a chord.")));
            }
        }

        return changes;
    }

    /// <summary>Feeds the estimates and counts the chord changes; no list, so allocation tests can measure it.</summary>
    public static int Run(Harmonizer harmonizer, PitchEstimate[] estimates)
    {
        int changes = 0;
        foreach (PitchEstimate estimate in estimates)
        {
            if (harmonizer.Process(estimate))
            {
                changes++;
            }
        }

        return changes;
    }

    /// <summary>The chord in effect at the end of every note (rests left out), as symbols.</summary>
    public static string[] ChordsAtNoteEnds(Harmonizer harmonizer, Note[] notes)
    {
        List<(double TimeMs, Chord Chord)> changes = Changes(harmonizer, notes);
        return [.. notes.Where(note => note.Name is not null)
            .Select(note => changes.Last(change => change.TimeMs < note.EndMs).Chord.Symbol)];
    }

    public static Harmonizer Harmonizer(IChordRules rules, string tonic, Mode mode = Mode.Ionian)
    {
        Assert.True(PitchClass.TryParse(tonic, out int pitchClass));
        return new Harmonizer(rules, new Key(pitchClass, mode), HarmonyTiming.FromSettings(TestSettings.Default));
    }

    private static int Midi(string name)
    {
        Assert.True(NoteName.TryParseMidiNumber(name.Any(char.IsDigit) ? name : $"{name}4", out int midi));
        return midi;
    }
}
