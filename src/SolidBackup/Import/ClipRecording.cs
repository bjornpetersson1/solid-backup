using SolidBackup.Recording;

namespace SolidBackup.Import;

/// <summary>
/// Saves an accompanied clip as a recording (E8-4): stereo 16-bit at the clip's rate, the clip on the left and the
/// chords on the right, as live mode records, so the recordings list and player handle it like any recording.
/// </summary>
public static class ClipRecording
{
    // Interleaved samples written at once.
    private const int Piece = 8192;

    // Characters of the clip's name kept in the result's: with the time and _2 after it, well within a file name's
    // 255 bytes, also with letters of several bytes.
    private const int MaxNameLength = 80;

    /// <summary>Saves to <c>&lt;clip name&gt;_&lt;time&gt;.wav</c> in the folder and returns its path.</summary>
    /// <exception cref="IOException">The folder or the file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder cannot be written.</exception>
    public static string Save(string folder, DateTime localNow, ImportedClip clip, float[] chords)
    {
        string name = clip.Name.Length > MaxNameLength ? clip.Name[..MaxNameLength] : clip.Name;
        (string path, FileStream stream) = RecordingFile.Create(folder, localNow, name);
        try
        {
            using StereoWavWriter writer = new(stream, clip.Audio.SampleRate, Piece);
            float[] voice = clip.Audio.Samples;
            float[] interleaved = new float[Piece];
            for (int start = 0; start < voice.Length; start += Piece / 2)
            {
                int frames = Math.Min(Piece / 2, voice.Length - start);
                for (int frame = 0; frame < frames; frame++)
                {
                    interleaved[2 * frame] = voice[start + frame];
                    interleaved[(2 * frame) + 1] = chords[start + frame];
                }

                writer.Write(interleaved.AsSpan(0, 2 * frames));
            }

            return path;
        }
        catch
        {
            stream.Dispose();
            File.Delete(path);
            throw;
        }
    }
}
