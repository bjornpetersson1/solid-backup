using SolidBackup.Analysis;
using SolidBackup.Configuration;
using SolidBackup.Harmony;
using SolidBackup.Synthesis;

namespace SolidBackup.Accompaniment;

/// <summary>
/// Renders chords one after the other, each for a fixed time from the start, at full strength, through the settings'
/// rhythm pattern on its grid, and ends with the sound's release.
/// </summary>
public static class ChordSequence
{
    // Rendered in pieces that start on every chord change, so a pattern sees each chord from its first sample.
    private const int MaxPiece = 4096;

    public static float[] Render(AppSettings settings, IReadOnlyList<Chord> chords, int chordMs)
    {
        Synthesizer synthesizer = Synthesizer.FromSettings(settings);
        IRhythmPattern pattern = RhythmPatterns.Create(settings);
        int chordSamples = (int)Duration.Samples(chordMs, settings.SampleRate);
        int end = checked(chords.Count * chordSamples);
        float[] samples = new float[checked(end + (int)Duration.Samples(synthesizer.Preset.PlayedReleaseMs, settings.SampleRate))];
        for (int start = 0; start < samples.Length;)
        {
            int index = start / chordSamples;
            int boundary = start < end ? (index + 1) * chordSamples : samples.Length;
            int length = Math.Min(MaxPiece, boundary - start);
            // Each listed chord is struck where it begins, so a repeated chord is heard again even with a sound that dies away.
            AccompanimentFrame frame = AccompanimentFrame.Neutral(start < end ? chords[index] : null, sounding: start < end) with
            {
                Strike = start < end && start % chordSamples == 0,
            };
            pattern.Play(AnalysisFrame.Position(start + length, length, settings.SampleRate), frame, synthesizer);
            synthesizer.Render(samples.AsSpan(start, length));
            start += length;
        }

        return samples;
    }
}
