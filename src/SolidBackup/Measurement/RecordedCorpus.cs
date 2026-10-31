using SolidBackup.Audio;

namespace SolidBackup.Measurement;

/// <summary>The recorded half of the corpus: every clip in the script with its WAV file and reference.</summary>
public static class RecordedCorpus
{
    public static IReadOnlyList<CorpusClip> Load(CorpusDirectory corpus)
    {
        return RecordingScript.Read(corpus.ScriptPath).Select(clip => Load(corpus, clip.Name)).ToArray();
    }

    public static CorpusClip Load(CorpusDirectory corpus, string clip)
    {
        string wavPath = corpus.WavPath(clip);
        WavAudio audio = WavReader.Read(wavPath);
        return new CorpusClip(clip, audio.SampleRate, audio.Samples, ReferenceFile.Read(corpus.ReferencePath(clip)), wavPath);
    }
}
