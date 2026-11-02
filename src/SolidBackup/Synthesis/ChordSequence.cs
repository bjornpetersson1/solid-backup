using SolidBackup.Analysis;
using SolidBackup.Harmony;

namespace SolidBackup.Synthesis;

/// <summary>Renders chords one after the other, each for a fixed time at full strength, ending with the sound's release.</summary>
public static class ChordSequence
{
    // Rendered in chunks, so only the chords that start in a chunk wait in the synthesizer's event queue.
    private const int ChunkSize = 4096;

    public static float[] Render(Sound sound, int sampleRate, IReadOnlyList<Chord> chords, int chordMs)
    {
        Synthesizer synthesizer = new(sound, sampleRate);
        int chordSamples = (int)Duration.Samples(chordMs, sampleRate);
        int end = checked(chords.Count * chordSamples);
        float[] samples = new float[checked(end + (int)Duration.Samples(synthesizer.Preset.PlayedReleaseMs, sampleRate))];
        int nextChord = 0;
        for (int start = 0; start < samples.Length; start += ChunkSize)
        {
            int length = Math.Min(ChunkSize, samples.Length - start);
            for (; nextChord < chords.Count && nextChord * chordSamples < start + length; nextChord++)
            {
                synthesizer.SetChord(chords[nextChord], strength: 1, (nextChord * chordSamples) - start);
            }

            if (end >= start && end < start + length)
            {
                synthesizer.ReleaseAll(end - start);
            }

            synthesizer.Render(samples.AsSpan(start, length));
        }

        return samples;
    }
}
